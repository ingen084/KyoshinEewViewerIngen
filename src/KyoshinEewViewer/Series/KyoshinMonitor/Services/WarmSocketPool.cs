using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Services;

/// <summary>
/// 指定されたエンドポイントに対し、確立済みの TCP ソケットを常に温めておくプール
/// <para>
/// <see cref="System.Net.Http.SocketsHttpHandler.ConnectCallback"/> から <see cref="TakeAsync"/> を呼ぶことで、
/// HttpClient が新規接続を要求した瞬間に「すでに connect 済み」のソケットを払い出す。
/// これにより SYN ドロップによるタイムアウトを背景タスクで吸収できる。
/// </para>
/// <para>
/// 生成直後は停止状態であり、<see cref="SetEnabled"/> で有効化されるまでソケットの補充は行わない。
/// 停止中も <see cref="TakeAsync"/> は利用でき、その場合は同期接続にフォールバックする。
/// </para>
/// </summary>
public sealed class WarmSocketPool : IDisposable
{
	// 内部定数
	private const int MinIntervalSamples = 2;
	private const int MaxIntervalSamples = 5;
	// 「次の払い出し予測時刻」より何秒前から補充を開始するか。
	// これを大きくすると補充失敗時のリトライ猶予 (= 次の払い出しまでの残り時間) が増える。
	private static readonly TimeSpan PreRefillMargin = TimeSpan.FromSeconds(20);
	private static readonly TimeSpan IntervalNoiseThreshold = TimeSpan.FromSeconds(10);

	private readonly DnsEndPoint _endpoint;
	private readonly WarmSocketPoolOptions _options;
	private readonly ILogger _logger;
	private readonly CancellationTokenSource _shutdownCts = new();
	private readonly Lock _stateLock = new();
	private readonly Func<EndPoint, CancellationToken, Task<Socket>> _connectAsync;
	private readonly TimeProvider _timeProvider;
	private readonly HttpRequestMetrics? _metrics;
	private readonly Task _maintenanceTask;
	// 以下の可変状態はすべて _stateLock で保護する。
	// null は停止中。停止時のキャンセルは、その稼働期間に開始した補充をすべて無効にする。
	private CancellationTokenSource? _activeCts;
	private PooledSocket? _slot;
	private bool _disposed;

	// 直近の払い出し履歴に基づく予測 (JIT 補充用)
	private DateTime _lastTakeTime;
	private readonly Queue<TimeSpan> _recentIntervals = new();

	// 補充失敗の連続回数 (段階的なバックオフ用)
	private int _consecutiveRefillFailures;
	// 次に補充を試みてよい時刻 (バックオフ中はこの時刻まで補充を抑止する)
	private DateTime _nextRefillAttemptUtc;

	public WarmSocketPool(DnsEndPoint endpoint, WarmSocketPoolOptions options, ILogger logger)
		: this(endpoint, options, logger, null)
	{
	}

	internal WarmSocketPool(DnsEndPoint endpoint, WarmSocketPoolOptions options, ILogger logger,
		Func<EndPoint, CancellationToken, Task<Socket>>? connectAsync, TimeProvider? timeProvider = null, HttpRequestMetrics? metrics = null)
	{
		_endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_connectAsync = connectAsync ?? CreateSocketAsync;
		_timeProvider = timeProvider ?? TimeProvider.System;
		_metrics = metrics;

		_maintenanceTask = Task.Run(() => MaintenanceLoopAsync(_shutdownCts.Token));
	}

	/// <summary>ソケットの補充を行っているか</summary>
	public bool IsEnabled { get { lock (_stateLock) return _activeCts is not null; } }

	/// <summary>
	/// プールの稼働状態を切り替える。同じ値を何度設定しても副作用はない。
	/// 停止時は保持中のソケットを即座に破棄し、以後メンテナンスループは補充を行わない。
	/// 有効化後は通常のメンテナンス周期で補充する。障害時のバックオフは維持する
	/// </summary>
	public void SetEnabled(bool enabled)
	{
		lock (_stateLock)
		{
			ObjectDisposedException.ThrowIf(_disposed, this);
			if (enabled == (_activeCts is not null)) return;
			if (enabled)
			{
				_activeCts = new CancellationTokenSource();
				// 休止時間を含めずに学習し直す。再開直後は予測待ちせず補充する。
				_lastTakeTime = default;
				_recentIntervals.Clear();
			}
			else
			{
				_activeCts!.Cancel();
				_activeCts.Dispose();
				_activeCts = null;
				DropSlot();
			}
		}
	}

	/// <summary>HttpClient の ConnectCallback から呼ばれる。プールから1ソケット払い出す。</summary>
	public async ValueTask<Socket> TakeAsync(DnsEndPoint requested, CancellationToken ct)
	{
		// 別ホスト宛のリクエストはそのまま即時接続にフォールバック (履歴更新もしない)
		if (!IsOurEndpoint(requested))
			return await ConnectWithMetricsAsync(requested, ct, "other-endpoint");

		PooledSocket? pooled = null;
		lock (_stateLock)
		{
			if (_activeCts is not null)
			{
				RecordTake();
				pooled = _slot;
				_slot = null;
			}
		}
		if (pooled is not null)
		{
			if (IsHealthy(pooled))
			{
				_metrics?.WarmHit();
				return pooled.Socket;
			}
			SafeDispose(pooled.Socket);
			_metrics?.WarmDiscarded();
		}

		// スロットが空 → 同期接続にフォールバック
		_metrics?.ColdConnect();
		return await ConnectWithMetricsAsync(_endpoint, ct, "on-demand");
	}

	/// <summary>払い出しの履歴を記録する。_stateLock 内で呼ぶ。</summary>
	private void RecordTake()
	{
		var now = _timeProvider.GetUtcNow().UtcDateTime;
		if (_lastTakeTime != default)
		{
			var interval = now - _lastTakeTime;
			// あまりに短い間隔 (IntervalNoiseThreshold 未満) はノイズとして予測サンプルから除外する
			if (interval >= IntervalNoiseThreshold)
			{
				_recentIntervals.Enqueue(interval);
				while (_recentIntervals.Count > MaxIntervalSamples)
					_recentIntervals.Dequeue();
			}
		}
		_lastTakeTime = now;
	}

	/// <summary>
	/// 「いま補充すべきか」を判定する。
	/// 履歴がない/サンプル不足の間は常に true (即補充)。
	/// 履歴が十分あれば「経過時間 >= 予測間隔 - PreRefillMargin」の時に true。
	/// _stateLock 内で呼ぶ。
	/// </summary>
	private bool ShouldRefillNow()
	{
		// サンプル数が不足 → 予測信頼性が低いので保守的に即補充
		if (_recentIntervals.Count < MinIntervalSamples) return true;

		// 最大 5 件の履歴から中央値を求める。予測値を別の状態として保持しない。
		var sorted = _recentIntervals.Order().ToArray();
		// 経過時間が「予測間隔 - 余裕」を超えたら補充タイミング
		var elapsed = _timeProvider.GetUtcNow().UtcDateTime - _lastTakeTime;
		var refillThreshold = sorted[sorted.Length / 2] - PreRefillMargin;
		if (refillThreshold < TimeSpan.Zero) refillThreshold = TimeSpan.Zero;
		return elapsed >= refillThreshold;
	}

	/// <summary>スロット内のソケットを取り外して破棄する。_stateLock 内で呼ぶ。</summary>
	private void DropSlot()
	{
		var current = _slot;
		_slot = null;
		if (current is null) return;
		_metrics?.WarmDiscarded();
		SafeDispose(current.Socket);
	}

	private bool IsOurEndpoint(DnsEndPoint requested)
		=> requested.Port == _endpoint.Port
		&& string.Equals(requested.Host, _endpoint.Host, StringComparison.OrdinalIgnoreCase);

	private bool IsHealthy(PooledSocket pooled)
	{
		// 1. 寿命チェック
		var age = _timeProvider.GetUtcNow().UtcDateTime - pooled.CreatedAt;
		if (age > _options.MaxAge)
			return false;

		// 2. リモートからの FIN 検知
		try
		{
			if (pooled.Socket.Poll(0, SelectMode.SelectRead) && pooled.Socket.Available == 0)
				return false;
			return pooled.Socket.Connected;
		}
		catch (ObjectDisposedException) { return false; }
		catch (SocketException) { return false; }
	}

	private async Task MaintenanceLoopAsync(CancellationToken ct)
	{
		while (!ct.IsCancellationRequested)
		{
			try
			{
				await TryRefillOnceAsync(ct);

				await Task.Delay(_options.MaintenanceInterval, ct);
			}
			catch (OperationCanceledException)
			{
				return;
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "WarmSocketPool メンテナンスタスクで予期しない例外");
				try { await Task.Delay(TimeSpan.FromSeconds(1), ct); }
				catch (OperationCanceledException) { return; }
			}
		}
	}

	/// <summary>
	/// スロットに保持しているソケットがリモートから close されているか MaxAge を超過していれば破棄する。
	/// MaxAge 超過分を Take 時ではなくここで破棄しておくことで、
	/// 払い出されるソケットが常に新鮮な状態 (張り替え済み) になる
	/// _stateLock 内で呼ぶ。
	/// </summary>
	private void CleanupDeadSocket()
	{
		var current = _slot;
		if (current is null) return;

		if (!IsHealthy(current))
			DropSlot();
	}

	private async Task TryRefillOnceAsync(CancellationToken ct)
	{
		CancellationTokenSource connectCts;
		CancellationToken activeToken;
		lock (_stateLock)
		{
			if (_activeCts is null) return;
			CleanupDeadSocket();
			if (_slot is not null
				|| _timeProvider.GetUtcNow().UtcDateTime < _nextRefillAttemptUtc
				|| !ShouldRefillNow())
				return;
			activeToken = _activeCts.Token;
			connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct, activeToken);
			connectCts.CancelAfter(_options.ConnectTimeout);
		}

		using var connectionTimeout = connectCts;
		Socket? socket = null;
		try
		{
			socket = await ConnectWithMetricsAsync(_endpoint, connectCts.Token, "refill", activeToken);
			lock (_stateLock)
			{
				// 停止・再開をまたいだ接続結果も格納しない。所有権が移らなかったソケットは finally で破棄する。
				if (activeToken.IsCancellationRequested || ct.IsCancellationRequested)
					return;
				connectCts.Token.ThrowIfCancellationRequested();
				_slot = new PooledSocket(socket, _timeProvider.GetUtcNow().UtcDateTime);
				socket = null;
				_consecutiveRefillFailures = 0;
				_nextRefillAttemptUtc = DateTime.MinValue;
			}
		}
		catch (Exception ex)
		{
			lock (_stateLock)
			{
				// 停止に伴う中断は接続障害として数えない。
				if (activeToken.IsCancellationRequested || ct.IsCancellationRequested)
					return;
				var failures = ++_consecutiveRefillFailures;
				var backoff = ComputeRefillBackoff(failures);
				_nextRefillAttemptUtc = _timeProvider.GetUtcNow().UtcDateTime + backoff;
				LogRefillFailure(failures, backoff, ex);
			}
		}
		finally
		{
			SafeDispose(socket);
		}
	}

	/// <summary>
	/// 連続失敗回数からバックオフ時間を計算する。±25% のランダム揺らぎが適用される。
	/// 3 回以下の失敗では追加の待ち時間を設けず、次のメンテナンス周期で再試行する。
	/// 長期間の障害 (回線切断等) では段階的に間隔を伸ばしてサーバ・ログ・CPU 負荷を抑える。
	/// </summary>
	private static TimeSpan ComputeRefillBackoff(int consecutiveFailures) => WithJitter(consecutiveFailures switch
	{
		<= 3 => TimeSpan.Zero,
		<= 10 => TimeSpan.FromSeconds(5),
		<= 30 => TimeSpan.FromSeconds(30),
		_ => TimeSpan.FromMinutes(5),
	});

	/// ±25% のランダムな揺らぎを加える
	private static TimeSpan WithJitter(TimeSpan delay)
		=> delay <= TimeSpan.Zero ? delay : delay * (0.75 + Random.Shared.NextDouble() * 0.5);

	/// <summary>連続失敗のログ出力。長時間の連続失敗時にはログを抑制する。</summary>
	private void LogRefillFailure(int failures, TimeSpan backoff, Exception ex)
	{
		// 出力する節目: 1〜5 回目、10、30、以降は 60 の倍数
		var shouldLog = failures <= 5
			|| failures == 10
			|| failures == 30
			|| (failures > 30 && failures % 60 == 0);
		if (!shouldLog) return;

		var backoffStr = backoff > TimeSpan.Zero
			? $"{backoff.TotalSeconds:F0} 秒経過後のメンテナンス"
			: "次のメンテナンス";

		// 4 回以上の連続失敗は Warning
		if (failures >= 4)
			_logger.LogWarning("ソケット補充失敗 (連続 {Failures} 回)、{BackoffStr}にリトライ: {Message}", failures, backoffStr, ex.Message);
		else
			_logger.LogDebug("ソケット補充失敗 (連続 {Failures} 回)、{BackoffStr}にリトライ: {Message}", failures, backoffStr, ex.Message);
	}

	private async Task<Socket> ConnectWithMetricsAsync(EndPoint endpoint, CancellationToken ct, string purpose, CancellationToken activeToken = default)
	{
		var started = Stopwatch.GetTimestamp();
		try
		{
			var socket = await _connectAsync(endpoint, ct);
			_metrics?.TcpCompleted(Stopwatch.GetElapsedTime(started).TotalMilliseconds, true, purpose == "refill");
			return socket;
		}
		catch (Exception ex)
		{
			// 受信停止や終了によるキャンセルは接続障害に含めない。
			if (!activeToken.IsCancellationRequested && !_shutdownCts.IsCancellationRequested)
			{
				var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
				_metrics?.TcpCompleted(elapsed, false, purpose == "refill");
				if (purpose != "refill")
					_logger.LogDebug("TCP接続失敗 request={RequestId} purpose={Purpose} endpoint={Endpoint} elapsedMs={ElapsedMs:F1} canceled={Canceled} error={Error}",
						HttpRequestDiagnostics.CurrentRequestId, purpose, endpoint, elapsed, ct.IsCancellationRequested, ex.Message);
			}
			throw;
		}
	}

	private async Task<Socket> CreateSocketAsync(EndPoint endpoint, CancellationToken ct)
	{
		var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
		try
		{
			// NAT のセッションテーブル維持とサイレント切断の検知のため TCP keepalive を有効化する
			socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
			socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, (int)_options.KeepAliveTime.TotalSeconds);
			socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, (int)_options.KeepAliveInterval.TotalSeconds);
			socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, _options.KeepAliveRetryCount);
			await socket.ConnectAsync(endpoint, ct);
			return socket;
		}
		catch
		{
			SafeDispose(socket);
			throw;
		}
	}

	private static void SafeDispose(Socket? socket)
	{
		if (socket is null) return;
		try { socket.Dispose(); } catch { }
	}

	public void Dispose()
	{
		lock (_stateLock)
		{
			if (_disposed) return;
			SetEnabled(false);
			_disposed = true;
		}

		try { _shutdownCts.Cancel(); } catch { }

		try { _maintenanceTask.Wait(TimeSpan.FromSeconds(2)); } catch {}

		// 接続処理の終了までキャンセルソースを保持する。
		_ = _maintenanceTask.ContinueWith(_ => _shutdownCts.Dispose(),
			CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
	}

	private sealed record PooledSocket(Socket Socket, DateTime CreatedAt);
}
