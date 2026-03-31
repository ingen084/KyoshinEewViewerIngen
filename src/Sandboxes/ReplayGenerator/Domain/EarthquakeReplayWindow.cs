using System;
using System.Text.Json;

namespace ReplayGenerator.Domain;

/// <summary>
/// 地震情報トリガーで生成するリプレイの時間窓（発震前〜報告後）。
/// マグニチュード・深さが取れる場合は窓を広げ、取れない場合は保守的な既定値を使う。
/// </summary>
public static class EarthquakeReplayWindow
{
	/// <summary>マグニチュード不明時の発震時刻より前（秒）</summary>
	private const int DefaultPreSeconds = 60;

	/// <summary>マグニチュード不明時の報告時刻より後（秒）</summary>
	private const int DefaultPostSeconds = 90;

	/// <summary>発震前の上限（秒）</summary>
	private const int MaxPreSeconds = 300;

	/// <summary>報告後の上限（秒）</summary>
	private const int MaxPostSeconds = 600;

	public static (int PreSeconds, int PostSeconds) ComputeMargins(string? recordJson)
	{
		var m = TryParseMagnitude(recordJson);
		var depthKm = TryParseDepthKm(recordJson);

		var pre = DefaultPreSeconds;
		var post = DefaultPostSeconds;

		// 浅い震源は初期揺れ・誤差を踏まえ発震前をやや長めに
		if (depthKm is >= 0 and < 30)
			pre += 30;

		// 規模に応じて続報・余震を多めに含める
		if (m is not null)
		{
			if (m >= 5.0)
				post += 60;
			if (m >= 6.0)
			{
				pre += 30;
				post += 60;
			}
			if (m >= 7.0)
			{
				pre += 30;
				post += 120;
			}
			if (m >= 8.0)
				post += 120;
		}

		pre = Math.Min(pre, MaxPreSeconds);
		post = Math.Min(post, MaxPostSeconds);
		return (pre, post);
	}

	private static double? TryParseMagnitude(string? json)
	{
		if (string.IsNullOrWhiteSpace(json) || json == "{}")
			return null;
		try
		{
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;
			if (!root.TryGetProperty("hypocenter", out var h))
				return null;
			if (!h.TryGetProperty("magnitude", out var mag))
				return null;
			if (!mag.TryGetProperty("value", out var v))
				return null;
			if (v.ValueKind == JsonValueKind.Number)
				return v.GetDouble();
			return null;
		}
		catch
		{
			return null;
		}
	}

	private static double? TryParseDepthKm(string? json)
	{
		if (string.IsNullOrWhiteSpace(json) || json == "{}")
			return null;
		try
		{
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;
			if (!root.TryGetProperty("hypocenter", out var h))
				return null;
			if (!h.TryGetProperty("depth", out var d))
				return null;
			if (!d.TryGetProperty("value", out var v))
				return null;
			if (v.ValueKind == JsonValueKind.Number)
				return v.GetDouble();
			return null;
		}
		catch
		{
			return null;
		}
	}
}
