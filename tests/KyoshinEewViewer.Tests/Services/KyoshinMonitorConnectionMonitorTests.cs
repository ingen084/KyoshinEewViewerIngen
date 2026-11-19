using KyoshinEewViewer.Series.KyoshinMonitor.Services;

namespace KyoshinEewViewer.Tests.Services;

public class KyoshinMonitorConnectionMonitorTests
{
	private sealed class ManualTimeProvider : TimeProvider
	{
		private long _ticks;
		public override long TimestampFrequency => TimeSpan.TicksPerSecond;
		public override long GetTimestamp() => _ticks;
		public void Advance(TimeSpan duration) => _ticks += duration.Ticks;
	}

	/// <summary>揺らぎのない (乱数 0.5) 状態で生成する</summary>
	private static (KyoshinMonitorConnectionMonitor monitor, ManualTimeProvider clock) Create(Func<double>? random = null)
	{
		var clock = new ManualTimeProvider();
		return (new KyoshinMonitorConnectionMonitor(clock, random ?? (() => 0.5)), clock);
	}

	/// <summary>指定した時間だけ待っても試行できず、その直後から試行できることを確かめる</summary>
	private static void AssertNextAttemptAfter(KyoshinMonitorConnectionMonitor monitor, ManualTimeProvider clock, TimeSpan delay)
	{
		clock.Advance(delay - TimeSpan.FromMilliseconds(1));
		Assert.False(monitor.ShouldAttempt());
		clock.Advance(TimeSpan.FromMilliseconds(1));
		Assert.True(monitor.ShouldAttempt());
	}

	[Fact(DisplayName = "失敗が30秒続くまでは毎回取得でき、30秒に達した失敗で切断状態に入る")]
	public void 失敗が30秒続くと切断状態に入る()
	{
		var (monitor, clock) = Create();

		for (var i = 0; i < 30; i++)
		{
			Assert.False(monitor.ReportFailure());
			Assert.False(monitor.IsDisconnected);
			Assert.True(monitor.ShouldAttempt());
			clock.Advance(TimeSpan.FromSeconds(1));
		}

		Assert.True(monitor.ReportFailure());
		Assert.True(monitor.IsDisconnected);
		Assert.False(monitor.ShouldAttempt());
		// すでに切断中であれば、続く失敗で再び切断に入ったとは扱わない
		clock.Advance(TimeSpan.FromSeconds(5));
		Assert.False(monitor.ReportFailure());
		Assert.True(monitor.IsDisconnected);
	}

	[Fact(DisplayName = "切断中の再接続間隔は5秒から倍々に延び、1分で頭打ちになる")]
	public void 再接続間隔が指数的に延びて上限で止まる()
	{
		var (monitor, clock) = Create();
		monitor.ReportFailure();
		clock.Advance(KyoshinMonitorConnectionMonitor.DisconnectThreshold);
		Assert.True(monitor.ReportFailure());

		var expected = new[] { 5, 10, 20, 40, 60, 60 }.Select(s => TimeSpan.FromSeconds(s)).ToArray();
		for (var i = 0; i < expected.Length; i++)
		{
			Assert.Equal(expected[i], monitor.NextRetryDelay);
			AssertNextAttemptAfter(monitor, clock, expected[i]);
			if (i < expected.Length - 1)
				Assert.False(monitor.ReportFailure());
		}
	}

	[Fact(DisplayName = "再接続間隔には±25%の揺らぎが加わり、揺らいでも1分を超えない")]
	public void 再接続間隔に揺らぎが加わる()
	{
		foreach (var random in new[] { 0.0, 1.0 })
		{
			var (monitor, clock) = Create(() => random);
			monitor.ReportFailure();
			clock.Advance(KyoshinMonitorConnectionMonitor.DisconnectThreshold);
			monitor.ReportFailure();

			// 最初の間隔 5秒 → 3.75〜6.25秒
			var first = TimeSpan.FromSeconds(random == 0.0 ? 3.75 : 6.25);
			Assert.Equal(first, monitor.NextRetryDelay);
			AssertNextAttemptAfter(monitor, clock, first);

			// 上限に達した後 (60秒) → 45〜60秒
			for (var i = 0; i < 4; i++)
				monitor.ReportFailure();
			Assert.Equal(TimeSpan.FromSeconds(random == 0.0 ? 45 : 60), monitor.NextRetryDelay);
		}
	}

	[Fact(DisplayName = "応答が返ると切断から復帰し、次の障害は30秒の判定と5秒の間隔からやり直す")]
	public void 応答で切断から復帰する()
	{
		var (monitor, clock) = Create();
		monitor.ReportFailure();
		clock.Advance(KyoshinMonitorConnectionMonitor.DisconnectThreshold);
		monitor.ReportFailure();
		clock.Advance(TimeSpan.FromSeconds(5));
		monitor.ReportFailure();
		clock.Advance(TimeSpan.FromSeconds(10));

		// 最初の失敗から復帰までの時間を返す
		Assert.Equal(TimeSpan.FromSeconds(45), monitor.ReportReachable());
		Assert.False(monitor.IsDisconnected);
		Assert.True(monitor.ShouldAttempt());
		// 切断していなければ復帰とは扱わない
		Assert.Null(monitor.ReportReachable());

		monitor.ReportFailure();
		clock.Advance(KyoshinMonitorConnectionMonitor.DisconnectThreshold - TimeSpan.FromSeconds(1));
		Assert.False(monitor.ReportFailure());
		clock.Advance(TimeSpan.FromSeconds(1));
		Assert.True(monitor.ReportFailure());
		Assert.Equal(TimeSpan.FromSeconds(5), monitor.NextRetryDelay);
	}

	[Fact(DisplayName = "閾値に達する前に応答が返れば、失敗の継続時間は数え直しになる")]
	public void 閾値前の応答で失敗の継続時間がリセットされる()
	{
		var (monitor, clock) = Create();
		monitor.ReportFailure();
		clock.Advance(TimeSpan.FromSeconds(29));
		monitor.ReportFailure();
		monitor.ReportReachable();

		clock.Advance(TimeSpan.FromSeconds(1));
		Assert.False(monitor.ReportFailure());
		clock.Advance(TimeSpan.FromSeconds(29));
		Assert.False(monitor.ReportFailure());
		Assert.False(monitor.IsDisconnected);
	}

	[Fact(DisplayName = "オフセット調整中の失敗だけでは切断せず、上限到達後の失敗で最初の失敗からの時間をもとに切断する")]
	public void オフセット調整中の失敗は最初の失敗から数える()
	{
		var (monitor, clock) = Create();

		// オフセットが上限に達するまでの404 (1100ms → 5000ms で39回)
		for (var i = 0; i < 39; i++)
		{
			Assert.False(monitor.ReportFailure(canDisconnect: false));
			Assert.False(monitor.IsDisconnected);
			Assert.True(monitor.ShouldAttempt());
			clock.Advance(TimeSpan.FromSeconds(1));
		}

		// 上限に達した後の最初の404で、最初の404から30秒以上経っているため切断する
		Assert.True(monitor.ReportFailure());
		Assert.True(monitor.IsDisconnected);
	}

	[Fact(DisplayName = "切断中にオフセット調整中の失敗が返ると、切断のまま間隔を延ばさず次の取得をすぐに許可する")]
	public void 切断中のオフセット調整では間隔を延ばさない()
	{
		var (monitor, clock) = Create();
		monitor.ReportFailure();
		clock.Advance(KyoshinMonitorConnectionMonitor.DisconnectThreshold);
		monitor.ReportFailure();
		clock.Advance(TimeSpan.FromSeconds(5));
		Assert.True(monitor.ShouldAttempt());

		Assert.False(monitor.ReportFailure(canDisconnect: false));
		Assert.True(monitor.IsDisconnected);
		Assert.True(monitor.ShouldAttempt());

		// 上限に達した後の失敗では、前回の間隔 (5秒) の倍から再開する
		Assert.False(monitor.ReportFailure());
		Assert.Equal(TimeSpan.FromSeconds(10), monitor.NextRetryDelay);
		Assert.False(monitor.ShouldAttempt());
	}

	[Fact(DisplayName = "時刻のずれは要求した画像の時刻とオフセットからサーバーの Date ヘッダーを引いて求め、Kind に左右されない")]
	public void 時刻のずれを求める()
	{
		// サーバーの時刻 12:00:00 (日本時間) = 03:00:00 UTC
		var serverDate = new DateTimeOffset(2026, 11, 19, 3, 0, 0, TimeSpan.Zero);
		var cases = new (DateTime requested, int offset, double expectedSeconds)[]
		{
			// オフセット5秒で 11:59:55 を要求 → 時刻は合っている
			(new DateTime(2026, 11, 19, 11, 59, 55), 5000, 0),
			// 12:00:00 を要求 → こちらが5秒進んでいる
			(new DateTime(2026, 11, 19, 12, 0, 0), 5000, 5),
			// Kind が Utc / Local でも日本時間の値として扱う
			(new DateTime(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc), 5000, 5),
			(new DateTime(2026, 11, 19, 12, 0, 0, DateTimeKind.Local), 5000, 5),
			// タイムシフトなど過去の画像を要求した場合は負の値になる
			(new DateTime(2026, 11, 19, 11, 50, 0), 5000, -595),
		};
		foreach (var (requested, offset, expectedSeconds) in cases)
			Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), KyoshinMonitorConnectionMonitor.EstimateClockAhead(requested, offset, serverDate));

		Assert.Null(KyoshinMonitorConnectionMonitor.EstimateClockAhead(new DateTime(2026, 11, 19, 12, 0, 0), 5000, null));
	}

	[Fact(DisplayName = "リセットすると切断中だったかを返し、初期状態に戻る")]
	public void リセットで初期状態に戻る()
	{
		var (monitor, clock) = Create();
		Assert.False(monitor.Reset());

		monitor.ReportFailure();
		clock.Advance(KyoshinMonitorConnectionMonitor.DisconnectThreshold);
		monitor.ReportFailure();

		Assert.True(monitor.Reset());
		Assert.False(monitor.IsDisconnected);
		Assert.True(monitor.ShouldAttempt());
		Assert.False(monitor.ReportFailure());
	}
}
