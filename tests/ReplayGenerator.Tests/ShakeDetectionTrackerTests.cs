using Microsoft.Extensions.Logging.Abstractions;
using ReplayGenerator.Infrastructure;
using ReplayGenerator.Models;
using ReplayGenerator.Services;

namespace ReplayGenerator.Tests;

[Collection("Redis")]
public sealed class ShakeDetectionTrackerTests
{
	private readonly RedisFixture _redis;

	public ShakeDetectionTrackerTests(RedisFixture redis)
	{
		_redis = redis;
	}

	[Fact(DisplayName = "30秒無音後に waiting に遷移し、その後待機秒経過で生成フラグが立つ")]
	public async Task AfterSilence_TransitionsToWaiting_ThenGenerate()
	{
		await _redis.FlushAsync();
		var time = new FakeTimeProvider();
		var state = new ValkeyStateManager(_redis.Multiplexer, NullLogger<ValkeyStateManager>.Instance);
		var tracker = new ShakeDetectionTracker(state, NullLogger<ShakeDetectionTracker>.Instance, time);

		Assert.True(await tracker.OnShakeDetected("shake-1"));

		time.Advance(TimeSpan.FromSeconds(29));
		var (a, _) = await tracker.CheckTimerAsync(null);
		Assert.False(a);

		time.Advance(TimeSpan.FromSeconds(2));
		var (b, _) = await tracker.CheckTimerAsync(null);
		Assert.False(b);

		time.Advance(TimeSpan.FromSeconds(30));
		var (c, st) = await tracker.CheckTimerAsync(null);
		Assert.True(c);
		Assert.NotNull(st);
		Assert.Equal("shake-1", st!.ShakeEventId);
		Assert.Equal(SessionStatus.Generating, st.Status);
	}
}
