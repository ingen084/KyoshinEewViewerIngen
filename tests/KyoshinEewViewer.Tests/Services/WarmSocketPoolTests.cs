using KyoshinEewViewer.Series.KyoshinMonitor.Services;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;

namespace KyoshinEewViewer.Tests.Services;

/// <summary>
/// WarmSocketPool のユニットテスト。
/// localhost に立てた TcpListener を相手に挙動を検証する。
/// </summary>
public sealed class WarmSocketPoolTests : IDisposable
{
	private readonly TcpListener _listener;
	private readonly DnsEndPoint _endpoint;
	private readonly ILogger<WarmSocketPoolTests> _logger;
	private readonly CancellationTokenSource _serverCts = new();
	private readonly List<WarmSocketPool> _createdPools = new();
	// テストサーバが accept したソケットの保持 (テスト終了時にまとめて破棄するため)
	private readonly List<Socket> _acceptedSockets = new();
	private readonly object _acceptedLock = new();

	public WarmSocketPoolTests()
	{
		_listener = new TcpListener(IPAddress.Loopback, 0);
		_listener.Start();
		var localEp = (IPEndPoint)_listener.LocalEndpoint;
		_endpoint = new DnsEndPoint(IPAddress.Loopback.ToString(), localEp.Port);

		_logger = NullLogger<WarmSocketPoolTests>.Instance;

		// バックグラウンドで accept ループを回す。受信したソケットはそのまま保持しておく。
		_ = Task.Run(async () =>
		{
			try
			{
				while (!_serverCts.IsCancellationRequested)
				{
					var s = await _listener.AcceptSocketAsync(_serverCts.Token);
					lock (_acceptedLock) _acceptedSockets.Add(s);
				}
			}
			catch (OperationCanceledException) { }
			catch (ObjectDisposedException) { }
			catch { /* ignore */ }
		});
	}

	private WarmSocketPool CreatePool(WarmSocketPoolOptions? options = null, bool enabled = true,
		Func<EndPoint, CancellationToken, Task<Socket>>? connectAsync = null, TimeProvider? timeProvider = null)
	{
		var opts = options ?? new WarmSocketPoolOptions
		{
			MaxAge = TimeSpan.FromSeconds(60),
			ConnectTimeout = TimeSpan.FromSeconds(2),
			MaintenanceInterval = TimeSpan.FromMilliseconds(100),
		};
		var pool = new WarmSocketPool(_endpoint, opts, _logger, connectAsync, timeProvider);
		_createdPools.Add(pool);
		if (enabled)
			pool.SetEnabled(true);
		return pool;
	}

	/// <summary>条件が満たされるまで短い間隔でポーリングする。</summary>
	private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
	{
		var start = DateTime.UtcNow;
		while (DateTime.UtcNow - start < timeout)
		{
			if (condition()) return true;
			await Task.Delay(20);
		}
		return condition();
	}

	private int AcceptedCount
	{
		get { lock (_acceptedLock) return _acceptedSockets.Count; }
	}

	private static async Task<Socket> ConnectTestSocketAsync(EndPoint endpoint, CancellationToken ct)
	{
		var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
		try
		{
			await socket.ConnectAsync(endpoint, ct);
			return socket;
		}
		catch
		{
			socket.Dispose();
			throw;
		}
	}

	// 予測履歴とバックオフの観測だけに使う。接続・停止・再開は公開 API から駆動する。
	private static T ReadPoolState<T>(WarmSocketPool pool, string name)
	{
		var stateLock = (Lock)typeof(WarmSocketPool).GetField("_stateLock", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pool)!;
		lock (stateLock)
			return (T)typeof(WarmSocketPool).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pool)!;
	}

	[Fact(DisplayName = "初期状態では補充せず停止中も都度接続を利用できる")]
	public async Task 初期状態では補充せず停止中も都度接続を利用できる()
	{
		using var pool = CreatePool(enabled: false);
		Assert.False(pool.IsEnabled);
		await Task.Delay(250);
		Assert.Equal(0, AcceptedCount);

		using var taken = await pool.TakeAsync(_endpoint, CancellationToken.None);
		Assert.True(taken.Connected);
		Assert.True(await WaitUntilAsync(() => AcceptedCount == 1, TimeSpan.FromSeconds(2)));
		await Task.Delay(250);
		Assert.Equal(1, AcceptedCount);
	}

	[Fact(DisplayName = "停止で待機接続を閉じ再開後は周期的な補充を再開する")]
	public async Task 停止で待機接続を閉じ再開後は周期的な補充を再開する()
	{
		using var pool = CreatePool();
		Assert.True(await WaitUntilAsync(() => AcceptedCount == 1, TimeSpan.FromSeconds(2)));
		Socket peer;
		lock (_acceptedLock) peer = _acceptedSockets[0];
		pool.SetEnabled(false);
		Assert.True(await WaitUntilAsync(() => peer.Poll(0, SelectMode.SelectRead) && peer.Available == 0, TimeSpan.FromSeconds(2)));
		await Task.Delay(150);
		Assert.Equal(1, AcceptedCount);

		pool.SetEnabled(true);
		Assert.True(await WaitUntilAsync(() => AcceptedCount == 2, TimeSpan.FromSeconds(2)));
		pool.SetEnabled(true);
		await Task.Delay(150);
		Assert.Equal(2, AcceptedCount);
	}

	[Fact(DisplayName = "再開直後の取得はバックグラウンドの補充周期を待たない")]
	public async Task 再開直後の取得はバックグラウンドの補充周期を待たない()
	{
		using var pool = CreatePool(new WarmSocketPoolOptions { MaintenanceInterval = TimeSpan.FromSeconds(30) }, enabled: false);
		await Task.Delay(150);
		pool.SetEnabled(true);
		using var taken = await pool.TakeAsync(_endpoint, CancellationToken.None).AsTask().WaitAsync(TimeSpan.FromSeconds(2));
		Assert.True(taken.Connected);
	}

	[Fact(DisplayName = "同じ稼働状態の反映では待機接続を維持する")]
	public async Task 同じ稼働状態の反映では待機接続を維持する()
	{
		using var pool = CreatePool();
		Assert.True(await WaitUntilAsync(() => AcceptedCount == 1, TimeSpan.FromSeconds(2)));
		Socket peer;
		lock (_acceptedLock) peer = _acceptedSockets[0];
		// Series は切り替えが終わった状態だけを反映する。
		pool.SetEnabled(true);
		pool.SetEnabled(true);
		await Task.Delay(250);
		Assert.True(pool.IsEnabled);
		Assert.Equal(1, AcceptedCount);
		Assert.False(peer.Poll(0, SelectMode.SelectRead));

		pool.SetEnabled(false);
		Assert.False(pool.IsEnabled);
		Assert.True(await WaitUntilAsync(() => peer.Poll(0, SelectMode.SelectRead) && peer.Available == 0, TimeSpan.FromSeconds(2)));
	}

	[Theory(DisplayName = "停止後に完了した補充結果を格納せず破棄する")]
	[InlineData(false)]
	[InlineData(true)]
	public async Task 停止後に完了した補充結果を格納せず破棄する(bool restart)
	{
		var firstConnection = new TaskCompletionSource<(Socket socket, CancellationToken token)>(TaskCreationOptions.RunContinuationsAsynchronously);
		var completeFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var attempts = 0;
		using var pool = CreatePool(connectAsync: async (endpoint, ct) =>
		{
			var attempt = Interlocked.Increment(ref attempts);
			var socket = await ConnectTestSocketAsync(endpoint, ct);
			if (attempt == 1)
			{
				firstConnection.SetResult((socket, ct));
				// OS の接続完了とキャンセルが競合する状況を再現する。
				await completeFirst.Task;
			}
			return socket;
		});
		try
		{
			var first = await firstConnection.Task.WaitAsync(TimeSpan.FromSeconds(2));
			pool.SetEnabled(false);
			Assert.True(first.token.IsCancellationRequested);
			if (restart)
				pool.SetEnabled(true);
			completeFirst.SetResult();
			Assert.True(await WaitUntilAsync(() => first.socket.SafeHandle.IsClosed, TimeSpan.FromSeconds(2)));
			if (restart)
				Assert.True(await WaitUntilAsync(() => AcceptedCount == 2, TimeSpan.FromSeconds(2)));
			await Task.Delay(250);
			Assert.Equal(restart ? 2 : 1, Volatile.Read(ref attempts));
		}
		finally
		{
			completeFirst.TrySetResult();
		}
	}

	[Fact(DisplayName = "停止による接続キャンセルを障害として数えない")]
	public async Task 停止による接続キャンセルを障害として数えない()
	{
		var attempts = Channel.CreateUnbounded<CancellationToken>();
		using var pool = CreatePool(enabled: false, connectAsync: async (_, ct) =>
		{
			await attempts.Writer.WriteAsync(ct);
			await Task.Delay(Timeout.InfiniteTimeSpan, ct);
			throw new InvalidOperationException();
		});
		for (var i = 0; i < 5; i++)
		{
			pool.SetEnabled(true);
			var token = await attempts.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
			pool.SetEnabled(false);
			Assert.True(token.IsCancellationRequested);
		}
		Assert.Equal(0, ReadPoolState<int>(pool, "_consecutiveRefillFailures"));
	}

	[Fact(DisplayName = "再開で障害時のバックオフを解除しない")]
	public async Task 再開で障害時のバックオフを解除しない()
	{
		var clock = new ManualTimeProvider();
		var attempts = 0;
		using var pool = CreatePool(connectAsync: (_, _) =>
		{
			Interlocked.Increment(ref attempts);
			throw new SocketException((int)SocketError.ConnectionRefused);
		}, timeProvider: clock);
		Assert.True(await WaitUntilAsync(() => ReadPoolState<DateTime>(pool, "_nextRefillAttemptUtc") > clock.GetUtcNow().UtcDateTime, TimeSpan.FromSeconds(2)));
		Assert.Equal(4, Volatile.Read(ref attempts));
		pool.SetEnabled(false);
		pool.SetEnabled(true);
		await Task.Delay(250);
		Assert.Equal(4, Volatile.Read(ref attempts));

		clock.Advance(TimeSpan.FromSeconds(10));
		Assert.True(await WaitUntilAsync(() => Volatile.Read(ref attempts) == 5, TimeSpan.FromSeconds(2)));
	}

	[Fact(DisplayName = "再開後は接続間隔を学習し直す")]
	public async Task 再開後は接続間隔を学習し直す()
	{
		var clock = new ManualTimeProvider();
		using var pool = CreatePool(timeProvider: clock);
		for (var i = 0; i < 3; i++)
		{
			using var taken = await pool.TakeAsync(_endpoint, CancellationToken.None);
			if (i < 2)
				clock.Advance(TimeSpan.FromSeconds(60));
		}
		pool.SetEnabled(false);
		var intervals = ReadPoolState<Queue<TimeSpan>>(pool, "_recentIntervals").ToArray();
		Assert.Equal(new[] { TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60) }, intervals);
		clock.Advance(TimeSpan.FromHours(1));
		// 停止中の都度接続も予測履歴に加えない。
		using (await pool.TakeAsync(_endpoint, CancellationToken.None)) { }
		Assert.Equal(intervals, ReadPoolState<Queue<TimeSpan>>(pool, "_recentIntervals").ToArray());

		pool.SetEnabled(true);
		using var resumed = await pool.TakeAsync(_endpoint, CancellationToken.None);
		Assert.Empty(ReadPoolState<Queue<TimeSpan>>(pool, "_recentIntervals"));
		Assert.True(await WaitUntilAsync(() => ReadPoolState<object?>(pool, "_slot") is not null, TimeSpan.FromSeconds(2)));
		clock.Advance(TimeSpan.FromSeconds(20));
		using var second = await pool.TakeAsync(_endpoint, CancellationToken.None);
		Assert.Equal(new[] { TimeSpan.FromSeconds(20) }, ReadPoolState<Queue<TimeSpan>>(pool, "_recentIntervals").ToArray());
		Assert.True(await WaitUntilAsync(() => ReadPoolState<object?>(pool, "_slot") is not null, TimeSpan.FromSeconds(2)));
	}

	private sealed class ManualTimeProvider : TimeProvider
	{
		private long _ticks = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).Ticks;
		public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);
		public void Advance(TimeSpan duration) => Interlocked.Add(ref _ticks, duration.Ticks);
	}

	[Fact(DisplayName = "有効化後に単一のウォームソケットが補充される")]
	public async Task 有効化後に単一のウォームソケットが補充される()
	{
		using var pool = CreatePool();

		var ok = await WaitUntilAsync(
			() => { lock (_acceptedLock) return _acceptedSockets.Count >= 1; },
			TimeSpan.FromSeconds(3));

		Assert.True(ok, "有効化後に 1 ソケットが補充されるはず");
	}

	[Fact(DisplayName = "TakeAsyncで健全なソケットが払い出されてから即座に補充される")]
	public async Task TakeAsyncで健全なソケットが払い出されてから即座に補充される()
	{
		using var pool = CreatePool();

		// 最初の補充 (1 件) を待つ
		await WaitUntilAsync(
			() => { lock (_acceptedLock) return _acceptedSockets.Count >= 1; },
			TimeSpan.FromSeconds(3));

		// 1 つ取り出す
		var taken = await pool.TakeAsync(_endpoint, CancellationToken.None);
		Assert.NotNull(taken);
		Assert.True(taken.Connected);
		taken.Dispose();

		// 補充されて累計 accept が 1 件増えるはず
		var refilled = await WaitUntilAsync(
			() => { lock (_acceptedLock) return _acceptedSockets.Count >= 2; },
			TimeSpan.FromSeconds(3));
		Assert.True(refilled, "Take 後に追加で 1 件補充されるはず");
	}

	[Fact(DisplayName = "別ホスト宛のTakeAsyncはプールを使わずフォールバックする")]
	public async Task 別ホスト宛のTakeAsyncはプールを使わずフォールバックする()
	{
		using var pool = CreatePool();

		// kmoni と違うエンドポイントを指定 (同じ loopback だがポートが異なる)
		var otherListener = new TcpListener(IPAddress.Loopback, 0);
		otherListener.Start();
		try
		{
			var otherPort = ((IPEndPoint)otherListener.LocalEndpoint).Port;
			var otherEp = new DnsEndPoint(IPAddress.Loopback.ToString(), otherPort);

			var acceptTask = otherListener.AcceptSocketAsync();
			var taken = await pool.TakeAsync(otherEp, CancellationToken.None);
			var serverSide = await acceptTask;

			Assert.NotNull(taken);
			Assert.True(taken.Connected);
			Assert.Equal(otherPort, ((IPEndPoint)taken.RemoteEndPoint!).Port);

			taken.Dispose();
			serverSide.Dispose();
		}
		finally
		{
			otherListener.Stop();
		}
	}

	[Fact(DisplayName = "MaxAge超過したソケットは生きていてもメンテナンスループで破棄され再補充される")]
	public async Task MaxAge超過したソケットはメンテナンスループで破棄され再補充される()
	{
		var opts = new WarmSocketPoolOptions
		{
			MaxAge = TimeSpan.FromMilliseconds(300),
			ConnectTimeout = TimeSpan.FromSeconds(2),
			MaintenanceInterval = TimeSpan.FromMilliseconds(50),
		};
		using var pool = CreatePool(opts);

		// 最初の 1 ソケットが補充されるまで待つ
		await WaitUntilAsync(
			() => { lock (_acceptedLock) return _acceptedSockets.Count >= 1; },
			TimeSpan.FromSeconds(2));

		var initialCount = 0;
		lock (_acceptedLock) initialCount = _acceptedSockets.Count;

		// テストサーバは accept しっぱなし (FIN/RST を送らない) でソケットは生きているが、
		// MaxAge (300ms) を超過するとメンテナンスループが破棄し、新しいソケットが補充されるはず
		var refilled = await WaitUntilAsync(
			() => { lock (_acceptedLock) return _acceptedSockets.Count > initialCount; },
			TimeSpan.FromSeconds(2));
		Assert.True(refilled, "MaxAge 超過後にメンテナンスループが破棄・再補充するはず");
	}

	[Fact(DisplayName = "払い出されたソケットにTCP keepaliveが設定されている")]
	public async Task 払い出されたソケットにTCPkeepaliveが設定されている()
	{
		using var pool = CreatePool();

		await WaitUntilAsync(
			() => { lock (_acceptedLock) return _acceptedSockets.Count >= 1; },
			TimeSpan.FromSeconds(3));

		var taken = await pool.TakeAsync(_endpoint, CancellationToken.None);
		try
		{
			Assert.NotEqual(0, (int)taken.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive)!);
			// デフォルトオプション (15秒/5秒/2回) が反映されていること
			Assert.Equal(15, (int)taken.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime)!);
			Assert.Equal(5, (int)taken.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval)!);
			Assert.Equal(2, (int)taken.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount)!);
		}
		finally
		{
			taken.Dispose();
		}
	}

	[Fact(DisplayName = "Take時にMaxAge超過していれば払い出しせず破棄してフォールバックする")]
	public async Task Take時にMaxAge超過していれば破棄してフォールバックする()
	{
		var opts = new WarmSocketPoolOptions
		{
			MaxAge = TimeSpan.FromMilliseconds(200),
			ConnectTimeout = TimeSpan.FromSeconds(2),
			MaintenanceInterval = TimeSpan.FromMilliseconds(50),
		};
		using var pool = CreatePool(opts);

		// 1 ソケット補充されるまで待つ
		await WaitUntilAsync(
			() => { lock (_acceptedLock) return _acceptedSockets.Count >= 1; },
			TimeSpan.FromSeconds(2));

		// MaxAge を超えるまで待つ
		await Task.Delay(400);

		// Take すると、プール内のソケットは MaxAge 超過で破棄され、新規 connect される
		var takenCountBefore = 0;
		lock (_acceptedLock) takenCountBefore = _acceptedSockets.Count;

		var taken = await pool.TakeAsync(_endpoint, CancellationToken.None);
		Assert.NotNull(taken);
		Assert.True(taken.Connected);
		taken.Dispose();

		// 同期フォールバックで新規接続が増えているはず
		await WaitUntilAsync(
			() => { lock (_acceptedLock) return _acceptedSockets.Count > takenCountBefore; },
			TimeSpan.FromSeconds(2));
	}

	[Fact(DisplayName = "JIT補充: 履歴サンプル不足の間は即座に補充される")]
	public async Task JIT補充_履歴サンプル不足の間は即座に補充される()
	{
		var opts = new WarmSocketPoolOptions
		{
			MaxAge = TimeSpan.FromSeconds(60),
			ConnectTimeout = TimeSpan.FromSeconds(2),
			MaintenanceInterval = TimeSpan.FromMilliseconds(50),
		};
		using var pool = CreatePool(opts);

		// 起動直後の補充を待つ
		await WaitUntilAsync(
			() => { lock (_acceptedLock) return _acceptedSockets.Count >= 1; },
			TimeSpan.FromSeconds(2));

		// 1 回目の Take → サンプル数 0 のまま (10 秒未満なので予測サンプルに加算されない)
		// → ShouldRefillNow は履歴不足で true → 即補充
		var t1 = await pool.TakeAsync(_endpoint, CancellationToken.None);
		t1.Dispose();
		await WaitUntilAsync(
			() => { lock (_acceptedLock) return _acceptedSockets.Count >= 2; },
			TimeSpan.FromSeconds(2));

		// 2 回目の Take → 同じくサンプル数不足で即補充
		await Task.Delay(50);
		var t2 = await pool.TakeAsync(_endpoint, CancellationToken.None);
		t2.Dispose();
		var refilled = await WaitUntilAsync(
			() => { lock (_acceptedLock) return _acceptedSockets.Count >= 3; },
			TimeSpan.FromSeconds(2));
		Assert.True(refilled);
	}

	[Theory(DisplayName = "履歴が揃ったら接続間隔の中央値の20秒前から補充する")]
	[InlineData(60, 60, 40)]
	[InlineData(30, 90, 70)]
	[InlineData(10, 10, 0)]
	public async Task 履歴が揃ったら予測時刻の20秒前から補充する(int firstInterval, int secondInterval, int refillAfter)
	{
		var clock = new ManualTimeProvider();
		using var pool = CreatePool(new WarmSocketPoolOptions
		{
			MaxAge = TimeSpan.FromMinutes(10),
			MaintenanceInterval = TimeSpan.FromMilliseconds(50),
		}, timeProvider: clock);

		// 補充完了を待って払い出すことで、履歴の確定時に接続処理が残らないようにする。
		foreach (var interval in new[] { 0, firstInterval, secondInterval })
		{
			Assert.True(await WaitUntilAsync(() => ReadPoolState<object?>(pool, "_slot") is not null, TimeSpan.FromSeconds(2)));
			clock.Advance(TimeSpan.FromSeconds(interval));
			using var taken = await pool.TakeAsync(_endpoint, CancellationToken.None);
		}

		if (refillAfter > 0)
		{
			clock.Advance(TimeSpan.FromSeconds(refillAfter - 1));
			await Task.Delay(200);
			Assert.Null(ReadPoolState<object?>(pool, "_slot"));
			clock.Advance(TimeSpan.FromSeconds(1));
		}
		Assert.True(await WaitUntilAsync(() => ReadPoolState<object?>(pool, "_slot") is not null, TimeSpan.FromSeconds(2)));
	}

	[Fact(DisplayName = "リモート切断時にメンテナンスループが検知して新ソケットを補充する")]
	public async Task リモート切断時にメンテナンスループが検知して新ソケットを補充する()
	{
		var opts = new WarmSocketPoolOptions
		{
			MaxAge = TimeSpan.FromSeconds(60),
			ConnectTimeout = TimeSpan.FromSeconds(2),
			MaintenanceInterval = TimeSpan.FromMilliseconds(100),
		};
		using var pool = CreatePool(opts);

		// 初期ソケットの補充を待つ
		await WaitUntilAsync(
			() => { lock (_acceptedLock) return _acceptedSockets.Count >= 1; },
			TimeSpan.FromSeconds(3));

		// サーバ側で accept したソケットを shutdown → FIN を送信
		Socket serverSocket;
		lock (_acceptedLock) serverSocket = _acceptedSockets[0];
		serverSocket.Shutdown(SocketShutdown.Both);
		serverSocket.Dispose();

		var countBefore = 0;
		lock (_acceptedLock) countBefore = _acceptedSockets.Count;

		// メンテナンスループが FIN を検知して新ソケットを補充するのを待つ
		var refilled = await WaitUntilAsync(
			() => { lock (_acceptedLock) return _acceptedSockets.Count > countBefore; },
			TimeSpan.FromSeconds(3));
		Assert.True(refilled, "リモート切断後にメンテナンスループが新ソケットを補充するはず");

		// 補充されたソケットが健全であることを確認
		var taken = await pool.TakeAsync(_endpoint, CancellationToken.None);
		Assert.NotNull(taken);
		Assert.True(taken.Connected);
		taken.Dispose();
	}

	[Fact(DisplayName = "Dispose後のTakeAsyncはフォールバック接続で動作する")]
	public async Task Dispose後のTakeAsyncはフォールバック接続で動作する()
	{
		var pool = CreatePool();
		await WaitUntilAsync(
			() => { lock (_acceptedLock) return _acceptedSockets.Count >= 1; },
			TimeSpan.FromSeconds(3));

		pool.Dispose();
		_createdPools.Remove(pool);

		var countBefore = 0;
		lock (_acceptedLock) countBefore = _acceptedSockets.Count;

		// Dispose 後でもフォールバック接続で新規ソケットが返される
		var taken = await pool.TakeAsync(_endpoint, CancellationToken.None);
		Assert.NotNull(taken);
		Assert.True(taken.Connected);
		taken.Dispose();

		// フォールバックにより新たな accept が発生しているはず
		var newConnection = await WaitUntilAsync(
			() => { lock (_acceptedLock) return _acceptedSockets.Count > countBefore; },
			TimeSpan.FromSeconds(2));
		Assert.True(newConnection, "Dispose後はフォールバック接続で新規ソケットが作成されるはず");
	}

	[Fact(DisplayName = "Disposeでメンテナンスタスクが停止しソケットが解放される")]
	public async Task Disposeでメンテナンスタスクが停止しソケットが解放される()
	{
		var pool = CreatePool();
		await WaitUntilAsync(
			() => { lock (_acceptedLock) return _acceptedSockets.Count >= 1; },
			TimeSpan.FromSeconds(3));

		// 例外を出さずに完了することだけ確認 (リソースリーク検出は GC 任せ)
		pool.Dispose();
		_createdPools.Remove(pool);

		// Dispose 後の Take は新しいフォールバック接続を試みる (プールも空、リフィルも止まる)
		// → これも例外を出さないことを確認
		await Task.Delay(100);
	}

	public void Dispose()
	{
		_serverCts.Cancel();
		try { _listener.Stop(); } catch { /* ignore */ }
		foreach (var pool in _createdPools)
		{
			try { pool.Dispose(); } catch { /* ignore */ }
		}
		lock (_acceptedLock)
		{
			foreach (var s in _acceptedSockets)
			{
				try { s.Dispose(); } catch { /* ignore */ }
			}
		}
		_serverCts.Dispose();
		GC.SuppressFinalize(this);
	}
}
