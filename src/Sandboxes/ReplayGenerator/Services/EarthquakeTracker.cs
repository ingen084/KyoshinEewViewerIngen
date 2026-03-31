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
			var root = doc.RootElement;
			if (TryParseIsoDateTime(root, "origin_time", out var o1))
				originTime = o1;
			else if (TryParseIsoDateTime(root, "originTime", out var o2))
				originTime = o2;

			if (TryParseIsoDateTime(root, "report_time", out var r1))
				reportTime = r1;
			else if (TryParseIsoDateTime(root, "reportTime", out var r2))
				reportTime = r2;
			else if (TryParseIsoDateTime(root, "arrival_time", out var a1))
				reportTime = a1;
			else if (TryParseIsoDateTime(root, "arrivalTime", out var a2))
				reportTime = a2;
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

	private static bool TryParseIsoDateTime(JsonElement root, string name, out DateTime utc)
	{
		utc = default;
		if (!root.TryGetProperty(name, out var el))
			return false;
		if (el.ValueKind != JsonValueKind.String)
			return false;
		var s = el.GetString();
		if (string.IsNullOrEmpty(s))
			return false;
		if (!DateTime.TryParse(s, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
			return false;
		utc = parsed.Kind == DateTimeKind.Utc ? parsed : parsed.ToUniversalTime();
		return true;
	}
}
