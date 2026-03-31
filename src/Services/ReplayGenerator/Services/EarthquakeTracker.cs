using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ReplayGenerator.Infrastructure;
using ReplayGenerator.Models;

namespace ReplayGenerator.Services;

public class EarthquakeTracker
{
	private readonly ValkeyStateManager _state;
	private readonly ILogger<EarthquakeTracker> _logger;

	public EarthquakeTracker(ValkeyStateManager state, ILogger<EarthquakeTracker> logger)
	{
		_state = state;
		_logger = logger;
	}

	/// <summary>
	/// 地震情報メッセージを受信した際の処理。
	/// 戻り値が非 null の場合はリプレイファイル生成を開始する。
	/// </summary>
	public async Task<EarthquakeState?> OnEarthquakeUpsert(string eventId, string recordJson)
	{
		var acquired = await _state.TryAcquireLock("earthquake", eventId, TimeSpan.FromMinutes(10));
		if (!acquired)
		{
			_logger.LogDebug($"地震情報のロック取得失敗（重複）: {eventId}");
			return null;
		}

		DateTime? originTime = null;
		var reportTime = DateTime.UtcNow;

		try
		{
			using var doc = JsonDocument.Parse(recordJson);
			if (doc.RootElement.TryGetProperty("originTime", out var ot) && DateTime.TryParse(ot.GetString(), out var parsed))
				originTime = parsed;
			if (doc.RootElement.TryGetProperty("reportTime", out var rt) && DateTime.TryParse(rt.GetString(), out var parsedRt))
				reportTime = parsedRt;
		}
		catch { }

		var state = new EarthquakeState
		{
			EventId = eventId,
			OriginTime = originTime,
			ReportTime = reportTime,
			HypocenterJson = recordJson,
			Status = SessionStatus.Generating,
		};

		await _state.SaveEarthquakeState(state);
		_logger.LogInformation($"地震情報トリガー: {eventId}");
		return state;
	}

	public async Task CompleteAsync(string eventId)
	{
		await _state.ReleaseLock("earthquake", eventId);
		await _state.ClearEarthquakeState(eventId);
	}
}
