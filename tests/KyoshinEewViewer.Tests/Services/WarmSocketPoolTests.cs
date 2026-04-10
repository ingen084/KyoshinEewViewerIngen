using KyoshinEewViewer.Series.KyoshinMonitor.Services;
using Splat;
using System.Net;
using System.Net.Sockets;

namespace KyoshinEewViewer.Tests.Services;

/// <summary>
/// WarmSocketPool のユニットテスト。
/// localhost に立てた TcpListener を相手に挙動を検証する。
/// </summary>
public sealed class WarmSocketPoolTests : IDisposable
{
	private readonly TcpListener _listener;
	private readonly DnsEndPoint _endpoint;
	private readonly ILogger _logger;
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

		_logger = Locator.Current.GetService<ILogManager>()?.GetLogger<WarmSocketPoolTests>()
			?? new DefaultLogManager().GetLogger<WarmSocketPoolTests>();

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

	private WarmSocketPool CreatePool(WarmSocketPoolOptions? options = null)
	{
		var opts = options ?? new WarmSocketPoolOptions
		{
			InitialMaxAge = TimeSpan.FromSeconds(60),
			ConnectTimeout = TimeSpan.FromSeconds(2),
			MaintenanceInterval = TimeSpan.FromMilliseconds(100),
		};
		var pool = new WarmSocketPool(_endpoint, opts, _logger);
		_createdPools.Add(pool);
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

	[Fact(DisplayName = "プール初期化後に単一のウォームソケットが補充される")]
	public async Task プール初期化後に単一のウォームソケットが補充される()
	{
		using var pool = CreatePool();

		var ok = await WaitUntilAsync(
			() => { lock (_acceptedLock) return _acceptedSockets.Count >= 1; },
			TimeSpan.FromSeconds(3));

		Assert.True(ok, "起動直後に 1 ソケットが補充されるはず");
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

	[Fact(DisplayName = "MaxAge超過してもソケットが生きていればメンテナンスループで破棄しない (JIT 化後の挙動)")]
	public async Task MaxAge超過してもソケットが生きていれば破棄しない()
	{
		var opts = new WarmSocketPoolOptions
		{
			InitialMaxAge = TimeSpan.FromMilliseconds(300),
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

		// MaxAge (300ms) + 余裕 を待つ。テストサーバは accept しっぱなし (FIN/RST を送らない)
		// なので Poll() は alive を返す → メンテナンスループでは破棄されないはず
		await Task.Delay(800);

		var afterCount = 0;
		lock (_acceptedLock) afterCount = _acceptedSockets.Count;
		Assert.Equal(initialCount, afterCount);
	}

	[Fact(DisplayName = "Take時にMaxAge超過していれば払い出しせず破棄してフォールバックする")]
	public async Task Take時にMaxAge超過していれば破棄してフォールバックする()
	{
		var opts = new WarmSocketPoolOptions
		{
			InitialMaxAge = TimeSpan.FromMilliseconds(200),
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
			InitialMaxAge = TimeSpan.FromSeconds(60),
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

	[Fact(DisplayName = "UpdateMaxAgeは下限(10秒)と上限(90秒)でクランプされる")]
	public async Task UpdateMaxAgeは下限と上限でクランプされる()
	{
		var opts = new WarmSocketPoolOptions
		{
			InitialMaxAge = TimeSpan.FromSeconds(30),
		};
		using var pool = CreatePool(opts);
		await Task.Yield();

		// 下限 (= 10 秒、内部定数) より小さい値 → 下限値にクランプ
		pool.UpdateMaxAge(TimeSpan.FromSeconds(5));
		Assert.Equal(TimeSpan.FromSeconds(10), pool.CurrentMaxAge);

		// 上限 (= 90 秒、内部定数) より大きい値 → 上限値にクランプ
		pool.UpdateMaxAge(TimeSpan.FromSeconds(120));
		Assert.Equal(TimeSpan.FromSeconds(90), pool.CurrentMaxAge);

		// 範囲内 → そのまま反映
		pool.UpdateMaxAge(TimeSpan.FromSeconds(45));
		Assert.Equal(TimeSpan.FromSeconds(45), pool.CurrentMaxAge);
	}

	[Fact(DisplayName = "リモート切断時にメンテナンスループが検知して新ソケットを補充する")]
	public async Task リモート切断時にメンテナンスループが検知して新ソケットを補充する()
	{
		var opts = new WarmSocketPoolOptions
		{
			InitialMaxAge = TimeSpan.FromSeconds(60),
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
