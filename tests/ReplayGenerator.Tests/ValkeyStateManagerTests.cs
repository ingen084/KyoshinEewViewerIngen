using Microsoft.Extensions.Logging.Abstractions;
using ReplayGenerator.Infrastructure;
using ReplayGenerator.Models;

namespace ReplayGenerator.Tests;

[Collection("Redis")]
public sealed class ValkeyStateManagerTests
{
	private readonly RedisFixture _redis;

	public ValkeyStateManagerTests(RedisFixture redis)
	{
		_redis = redis;
	}

	[Fact(DisplayName = "揺れロック取得と解放が動作する")]
	public async Task ShakeLock_AcquireRelease_Works()
	{
		await _redis.FlushAsync();
		var mgr = new ValkeyStateManager(_redis.Multiplexer, NullLogger<ValkeyStateManager>.Instance);

		Assert.True(await mgr.TryAcquireLock("shake", "evt-a", TimeSpan.FromMinutes(1)));
		Assert.False(await mgr.TryAcquireLock("shake", "evt-a", TimeSpan.FromMinutes(1)));

		await mgr.ReleaseLock("shake", "evt-a");
		Assert.True(await mgr.TryAcquireLock("shake", "evt-a", TimeSpan.FromMinutes(1)));
	}

	[Fact(DisplayName = "揺れ状態の保存と読み込みが往復する")]
	public async Task ShakeState_SaveLoad_Roundtrips()
	{
		await _redis.FlushAsync();
		var mgr = new ValkeyStateManager(_redis.Multiplexer, NullLogger<ValkeyStateManager>.Instance);

		var state = new ShakeState
		{
			ShakeEventId = "evt-b",
			StartTime = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
			LastEventTime = new DateTime(2026, 3, 1, 0, 0, 10, DateTimeKind.Utc),
			Status = SessionStatus.Tracking,
			EewJson = null,
		};

		await mgr.SaveShakeState(state);
		var loaded = await mgr.LoadShakeState();

		Assert.NotNull(loaded);
		Assert.Equal(state.ShakeEventId, loaded!.ShakeEventId);
		Assert.Equal(state.Status, loaded.Status);
		Assert.Equal(state.StartTime, loaded.StartTime);
		Assert.Equal(state.LastEventTime, loaded.LastEventTime);

		await mgr.ClearShakeState();
		Assert.Null(await mgr.LoadShakeState());
	}
}
