using KyoshinEewViewer.Core;
using System;
using System.Collections.Generic;
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
/// </summary>
public sealed class WarmSocketPool : IDisposable
{
	// 内部定数
	private const int MinIntervalSamples = 2;
	private const int MaxIntervalSamples = 5;
	private static readonly TimeSpan MinMaxAge = TimeSpan.FromSeconds(10);
	private static readonly TimeSpan MaxMaxAge = TimeSpan.FromSeconds(90);
	// 「次の払い出し予測時刻」より何秒前から補充を開始するか。
	// これを大きくすると補充失敗時のリトライ猶予 (= 次の払い出しまでの残り時間) が増える。
	private static readonly TimeSpan PreRefillMargin = TimeSpan.FromSeconds(20);
	private static readonly TimeSpan IntervalNoiseThreshold = TimeSpan.FromSeconds(10);

	private readonly DnsEndPoint _endpoint;
	private readonly WarmSocketPoolOptions _options;
	private readonly ILogger _logger;
	private readonly CancellationTokenSource _shutdownCts = new();
	private readonly Task _maintenanceTask;

	// 単一スロット。Interlocked.Exchange / CompareExchange で操作する。
	private PooledSocket? _slot;

	// MaxAge は long(Ticks) で保持し、Interlocked.Read/Exchange でロックフリーに更新する
	private long _maxAgeTicks;
	private bool _disposed;

	// 直近の払い出し履歴に基づく予測 (JIT 補充用)
	private readonly Lock _historyLock = new();
	private DateTime _lastTakeTime;
	private readonly Queue<TimeSpan> _recentIntervals = new();
	private TimeSpan _predictedInterval;

	// 補充失敗の連続回数 (指数バックオフ用)
	private int _consecutiveRefillFailures;
	// 次に補充を試みてよい時刻 (バックオフ中はこの時刻まで補充を抑止する)
	private DateTime _nextRefillAttemptUtc;

	public WarmSocketPool(DnsEndPoint endpoint, WarmSocketPoolOptions options, ILogger logger)
	{
		_endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		_maxAgeTicks = _options.InitialMaxAge.Ticks;

		_maintenanceTask = Task.Run(() => MaintenanceLoopAsync(_shutdownCts.Token));
	}

	/// <summary>現在の MaxAge 値 (動的更新される)。</summary>
	public TimeSpan CurrentMaxAge => TimeSpan.FromTicks(Interlocked.Read(ref _maxAgeTicks));

	/// <summary>HttpClient の ConnectCallback から呼ばれる。プールから1ソケット払い出す。</summary>
	public async ValueTask<Socket> TakeAsync(DnsEndPoint requested, CancellationToken ct)
	{
		// 別ホスト宛のリクエストはそのまま即時接続にフォールバック (履歴更新もしない)
		if (!IsOurEndpoint(requested))
			return await CreateSocketAsync(requested, ct);

		// 自ホストへの払い出しは履歴更新の対象
		RecordTake();

		// 単一スロットからアトミックに取り出す
		var pooled = Interlocked.Exchange(ref _slot, null);
		if (pooled is not null)
		{
			if (IsHealthy(pooled))
			{
				_logger.LogDebug("ウォームソケットを払い出し");
				return pooled.Socket;
			}
			SafeDispose(pooled.Socket);
			_logger.LogDebug("プール内のソケットが MaxAge 超過/死活NG → 破棄");
		}

		// スロットが空 → 同期接続にフォールバック
		_logger.LogDebug("ウォームプール枯渇 → 同期接続にフォールバック");
		return await CreateSocketAsync(_endpoint, ct);
	}

	/// <summary>払い出しの履歴を記録し、予測間隔を更新する。</summary>
	private void RecordTake()
	{
		lock (_historyLock)
		{
			var now = DateTime.UtcNow;
			if (_lastTakeTime != default)
			{
				var interval = now - _lastTakeTime;
				// あまりに短い間隔 (IntervalNoiseThreshold 未満) はノイズとして予測サンプルから除外する
				if (interval >= IntervalNoiseThreshold)
				{
					_recentIntervals.Enqueue(interval);
					while (_recentIntervals.Count > MaxIntervalSamples)
						_recentIntervals.Dequeue();
					UpdatePredictedInterval();
				}
			}
			_lastTakeTime = now;
		}
	}

	private void UpdatePredictedInterval()
	{
		// _historyLock 内で呼ばれる前提
		if (_recentIntervals.Count == 0)
		{
			_predictedInterval = TimeSpan.Zero;
			return;
		}
		// 中央値を採用 (外れ値に強い)
		var sorted = _recentIntervals.OrderBy(x => x.Ticks).ToArray();
		var prev = _predictedInterval;
		_predictedInterval = sorted[sorted.Length / 2];
		if (Math.Abs((prev - _predictedInterval).TotalSeconds) >= 1.0)
			_logger.LogDebug("予測間隔: {TotalSeconds:F0}秒 → {TotalSeconds2:F0}秒 (サンプル数 {Count})", prev.TotalSeconds, _predictedInterval.TotalSeconds, _recentIntervals.Count);
	}

	/// <summary>
	/// 「いま補充すべきか」を判定する。
	/// 履歴がない/サンプル不足の間は常に true (即補充)。
	/// 履歴が十分あれば「経過時間 >= 予測間隔 - PreRefillMargin」の時に true。
	/// </summary>
	private bool ShouldRefillNow()
	{
		lock (_historyLock)
		{
			// 履歴なし → 起動直後なので即補充
			if (_lastTakeTime == default) return true;

			// サンプル数が不足 → 予測信頼性が低いので保守的に即補充
			if (_recentIntervals.Count < MinIntervalSamples) return true;

			// 経過時間が「予測間隔 - 余裕」を超えたら補充タイミング
			var elapsed = DateTime.UtcNow - _lastTakeTime;
			var refillThreshold = _predictedInterval - PreRefillMargin;
			if (refillThreshold < TimeSpan.Zero) refillThreshold = TimeSpan.Zero;
			return elapsed >= refillThreshold;
		}
	}

	/// <summary>
	/// スロット内のソケットを即座に破棄する。
	/// 取得タイムアウトの発生時など、プール内のソケットも死んでいる疑いが強い場合に呼ぶ。
	/// 破棄後の補充はメンテナンスループに任せる
	/// </summary>
	public void Flush()
	{
		var current = Interlocked.Exchange(ref _slot, null);
		if (current is null) return;
		SafeDispose(current.Socket);
		_logger.LogInformation("プール内のソケットを破棄しました (フラッシュ)");
	}

	internal void UpdateMaxAge(TimeSpan newMaxAge)
	{
		var clamped = TimeSpan.FromSeconds(Math.Clamp(
			newMaxAge.TotalSeconds,
			MinMaxAge.TotalSeconds,
			MaxMaxAge.TotalSeconds));

		var prev = TimeSpan.FromTicks(Interlocked.Exchange(ref _maxAgeTicks, clamped.Ticks));
		if (Math.Abs((prev - clamped).TotalSeconds) >= 1.0)
			_logger.LogInformation("WarmSocketPool MaxAge: {TotalSeconds:F0}秒 → {TotalSeconds2:F0}秒", prev.TotalSeconds, clamped.TotalSeconds);
	}

	private bool IsOurEndpoint(DnsEndPoint requested)
		=> requested.Port == _endpoint.Port
		&& string.Equals(requested.Host, _endpoint.Host, StringComparison.OrdinalIgnoreCase);

	private bool IsHealthy(PooledSocket pooled)
	{
		// 1. 寿命チェック
		var age = DateTime.UtcNow - pooled.CreatedAt;
		if (age > CurrentMaxAge)
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
				// Poll ベースで死んだソケットを掃除する
				CleanupDeadSocket();

				// スロットが空かつバックオフ時刻を過ぎており、JIT 補充の判定が真であれば 1 回だけ接続試行する。
				// 失敗時の再試行は「次サイクルで _nextRefillAttemptUtc を見て判断」する形で外側のループに任せる。
				if (Volatile.Read(ref _slot) is null
					&& DateTime.UtcNow >= _nextRefillAttemptUtc
					&& ShouldRefillNow())
				{
					await TryRefillOnceAsync(ct);
				}

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
	/// </summary>
	private void CleanupDeadSocket()
	{
		var current = Volatile.Read(ref _slot);
		if (current is null) return;

		var isExpired = DateTime.UtcNow - current.CreatedAt > CurrentMaxAge;
		if (!isExpired && IsAlive(current.Socket)) return;

		// 自分が観測した参照値が今もスロットにいる場合のみ取り外す (Take と競合しても安全)
		if (Interlocked.CompareExchange(ref _slot, null, current) == current)
		{
			SafeDispose(current.Socket);
			if (isExpired)
				_logger.LogDebug("MaxAge を超過したソケットを破棄しました");
			else
				_logger.LogInformation("死んだソケットを破棄しました");
		}
	}

	private static bool IsAlive(Socket socket)
	{
		try
		{
			if (socket.Poll(0, SelectMode.SelectRead) && socket.Available == 0)
				return false;
			return socket.Connected;
		}
		catch (ObjectDisposedException) { return false; }
		catch (SocketException) { return false; }
	}

	private async Task TryRefillOnceAsync(CancellationToken ct)
	{
		Socket? socket = null;
		try
		{
			using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			connectCts.CancelAfter(_options.ConnectTimeout);
			socket = await CreateSocketAsync(_endpoint, connectCts.Token);

			var newPooled = new PooledSocket(socket, DateTime.UtcNow);
			if (Interlocked.CompareExchange(ref _slot, newPooled, null) is null)
			{
				// 成功 → 連続失敗カウンタとバックオフをリセット
				var prevFailures = Interlocked.Exchange(ref _consecutiveRefillFailures, 0);
				_nextRefillAttemptUtc = DateTime.MinValue;
				if (prevFailures >= 4)
					_logger.LogInformation("ソケットを補充 ({PrevFailures} 回の連続失敗から復旧)", prevFailures);
				else
					_logger.LogDebug("ソケットを補充");
			}
			else
			{
				// 競合 (他経路で先にスロットが埋まった場合)。実運用では起こらないが念のため破棄。
				SafeDispose(socket);
			}
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			SafeDispose(socket);
		}
		catch (Exception ex)
		{
			SafeDispose(socket);
			var failures = Interlocked.Increment(ref _consecutiveRefillFailures);
			var backoff = ComputeRefillBackoff(failures);

			// ログを抑制: 失敗の最初の数回と、節目 (10/30/60/...) のみログを出す
			LogRefillFailure(failures, backoff, ex);

			// 次回試行可能時刻を設定 (次のメンテナンスサイクルが時刻を見て待機する)
			_nextRefillAttemptUtc = DateTime.UtcNow + backoff;
		}
	}

	/// <summary>
	/// 連続失敗回数からバックオフ時間を計算する。±25% のランダム揺らぎが適用される。
	/// 短期間の SYN ドロップ burst (3 回以下) には即時リトライで素早く回復し、
	/// 長期間の障害 (回線切断等) では指数的に間隔を伸ばしてサーバ・ログ・CPU 負荷を抑える。
	/// </summary>
	private static TimeSpan ComputeRefillBackoff(int consecutiveFailures) => WithJitter(consecutiveFailures switch
	{
		<= 3 => TimeSpan.Zero,                  // 即時リトライ
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
		// 出力する節目: 1〜5 回目、10、30、60、120、240... (60 の倍数)
		var shouldLog = failures <= 5
			|| failures == 10
			|| failures == 30
			|| (failures > 30 && failures % 60 == 0);
		if (!shouldLog) return;

		var backoffStr = backoff > TimeSpan.Zero
			? $"{backoff.TotalSeconds:F0} 秒後"
			: "即座";

		// 4 回以上の連続失敗は Warning
		if (failures >= 4)
			_logger.LogWarning("ソケット補充失敗 (連続 {Failures} 回)、{BackoffStr}にリトライ: {Message}", failures, backoffStr, ex.Message);
		else
			_logger.LogDebug("ソケット補充失敗 (連続 {Failures} 回)、{BackoffStr}にリトライ: {Message}", failures, backoffStr, ex.Message);
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
		if (_disposed) return;
		_disposed = true;

		try { _shutdownCts.Cancel(); } catch { }

		try { _maintenanceTask.Wait(TimeSpan.FromSeconds(2)); } catch {}

		var leftover = Interlocked.Exchange(ref _slot, null);
		if (leftover is not null)
			SafeDispose(leftover.Socket);

		_shutdownCts.Dispose();
	}

	private sealed record PooledSocket(Socket Socket, DateTime CreatedAt);
}
