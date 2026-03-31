using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ReplayGenerator.Models;
using StackExchange.Redis;

namespace ReplayGenerator.Infrastructure;

public class ValkeyStateManager
{
	private readonly IConnectionMultiplexer _redis;
	private readonly ILogger<ValkeyStateManager> _logger;

	public ValkeyStateManager(IConnectionMultiplexer redis, ILogger<ValkeyStateManager> logger)
	{
		_redis = redis;
		_logger = logger;
	}

	private IDatabase Db => _redis.GetDatabase();

	public async Task<bool> TryAcquireLock(string type, string eventId, TimeSpan expiry)
	{
		var key = $"replay:lock:{type}:{eventId}";
		return await Db.StringSetAsync(key, "1", expiry, When.NotExists);
	}

	public async Task ReleaseLock(string type, string eventId)
	{
		var key = $"replay:lock:{type}:{eventId}";
		await Db.KeyDeleteAsync(key);
	}

	public async Task SaveShakeState(ShakeState state)
	{
		var key = "replay:active:shake";
		var entries = new HashEntry[]
		{
			new("shakeEventId", state.ShakeEventId),
			new("startTime", state.StartTime.ToString("O")),
			new("lastEventTime", state.LastEventTime.ToString("O")),
			new("eewJson", state.EewJson ?? ""),
			new("status", state.Status.ToString()),
		};
		await Db.HashSetAsync(key, entries);
	}

	public async Task<ShakeState?> LoadShakeState()
	{
		var key = "replay:active:shake";
		var entries = await Db.HashGetAllAsync(key);
		if (entries.Length == 0) return null;

		var dict = entries.ToDictionary(e => e.Name.ToString(), e => e.Value.ToString());
		return new ShakeState
		{
			ShakeEventId = dict.GetValueOrDefault("shakeEventId", ""),
			StartTime = DateTime.TryParse(dict.GetValueOrDefault("startTime"), out var st) ? st : DateTime.UtcNow,
			LastEventTime = DateTime.TryParse(dict.GetValueOrDefault("lastEventTime"), out var let2) ? let2 : DateTime.UtcNow,
			EewJson = dict.GetValueOrDefault("eewJson") is { Length: > 0 } eew ? eew : null,
			Status = Enum.TryParse<SessionStatus>(dict.GetValueOrDefault("status"), out var s) ? s : SessionStatus.Tracking,
		};
	}

	public async Task ClearShakeState()
	{
		await Db.KeyDeleteAsync("replay:active:shake");
	}

	public async Task SaveEarthquakeState(EarthquakeState state)
	{
		var key = $"replay:active:earthquake:{state.EventId}";
		var entries = new HashEntry[]
		{
			new("eventId", state.EventId),
			new("originTime", state.OriginTime?.ToString("O") ?? ""),
			new("reportTime", state.ReportTime.ToString("O")),
			new("hypocenterJson", state.HypocenterJson ?? ""),
			new("status", state.Status.ToString()),
		};
		await Db.HashSetAsync(key, entries);
		await Db.KeyExpireAsync(key, TimeSpan.FromHours(1));
	}

	public async Task ClearEarthquakeState(string eventId)
	{
		await Db.KeyDeleteAsync($"replay:active:earthquake:{eventId}");
	}

	public async Task<List<EarthquakeState>> LoadAllEarthquakeStates()
	{
		var server = _redis.GetServers().First();
		var keys = server.Keys(pattern: "replay:active:earthquake:*").ToArray();
		var states = new List<EarthquakeState>();

		foreach (var key in keys)
		{
			var entries = await Db.HashGetAllAsync(key);
			if (entries.Length == 0) continue;

			var dict = entries.ToDictionary(e => e.Name.ToString(), e => e.Value.ToString());
			states.Add(new EarthquakeState
			{
				EventId = dict.GetValueOrDefault("eventId", ""),
				OriginTime = DateTime.TryParse(dict.GetValueOrDefault("originTime"), out var ot) ? ot : null,
				ReportTime = DateTime.TryParse(dict.GetValueOrDefault("reportTime"), out var rt) ? rt : DateTime.UtcNow,
				HypocenterJson = dict.GetValueOrDefault("hypocenterJson") is { Length: > 0 } h ? h : null,
				Status = Enum.TryParse<SessionStatus>(dict.GetValueOrDefault("status"), out var s) ? s : SessionStatus.Tracking,
			});
		}

		return states;
	}

	/// <summary>
	/// Valkey の realtime:snapshot:v2 キーからスナップショットを取得
	/// </summary>
	public async Task<string?> GetRealtimeSnapshot()
	{
		return await Db.StringGetAsync("realtime:snapshot:v2");
	}
}
