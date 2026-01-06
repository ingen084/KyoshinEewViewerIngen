using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using KyoshinEewViewer.Core.Models;
using ShakeDetectionProducer.Services;

namespace ShakeDetectionProducer;

/// <summary>
/// Valkey Streamで送信するペイロードの基底クラス
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ShakeDetectedPayload), "shake_detected")]
[JsonDerivedType(typeof(ErrorPayload), "error")]
public abstract record StreamPayload;

/// <summary>
/// 揺れ検知イベントペイロード
/// </summary>
public record ShakeDetectedPayload : StreamPayload
{
	public required Guid EventId { get; init; }
	public required DateTime CreatedAt { get; init; }
	public required string Level { get; init; }
	/// <summary>
	/// 変化理由の配列
	/// </summary>
	public required string[] ChangeReasons { get; init; }
	public required bool IsReplay { get; init; }
	public required int PointCount { get; init; }
	public required RegionPayload Region { get; init; }
	public required ObservationPointPayload[] Points { get; init; }

	public static ShakeDetectedPayload FromEvent(KyoshinEvent evt, EventChangeReason changeReason, bool isReplay)
	{
		return new ShakeDetectedPayload
		{
			EventId = evt.Id,
			CreatedAt = evt.CreatedAt,
			Level = evt.Level.ToString(),
			ChangeReasons = GetChangeReasonStrings(changeReason),
			IsReplay = isReplay,
			PointCount = evt.PointCount,
			Region = new RegionPayload
			{
				TopLeft = new LocationPayload
				{
					Latitude = evt.TopLeft.Latitude,
					Longitude = evt.TopLeft.Longitude
				},
				BottomRight = new LocationPayload
				{
					Latitude = evt.BottomRight.Latitude,
					Longitude = evt.BottomRight.Longitude
				}
			},
			Points = evt.Points.Select(p => new ObservationPointPayload
			{
				Code = p.Code,
				Name = p.Name,
				Region = p.Region,
				Type = p.Type.ToString(),
				Location = new LocationPayload
				{
					Latitude = p.Location.Latitude,
					Longitude = p.Location.Longitude
				},
				Intensity = p.LatestIntensity,
				IntensityDiff = p.IntensityDiff
			}).ToArray()
		};
	}

	private static string[] GetChangeReasonStrings(EventChangeReason reason)
	{
		var reasons = new List<string>();
		if (reason.HasFlag(EventChangeReason.NewEvent))
			reasons.Add("new_event");
		if (reason.HasFlag(EventChangeReason.LevelUp))
			reasons.Add("level_up");
		if (reason.HasFlag(EventChangeReason.LevelDown))
			reasons.Add("level_down");
		if (reason.HasFlag(EventChangeReason.RegionChanged))
			reasons.Add("region_changed");
		if (reason.HasFlag(EventChangeReason.PointsChanged))
			reasons.Add("points_changed");
		return reasons.ToArray();
	}
}

/// <summary>
/// エラーペイロード
/// </summary>
public record ErrorPayload : StreamPayload
{
	public required string ErrorType { get; init; }
	public required DateTime Time { get; init; }
	public required string Message { get; init; }

	public static ErrorPayload Timeout(DateTime time)
		=> new()
		{
			ErrorType = "timeout",
			Time = time,
			Message = "強震モニタからのデータ取得がタイムアウトしました"
		};

	public static ErrorPayload HttpError(DateTime time, string? details = null)
		=> new()
		{
			ErrorType = "http_error",
			Time = time,
			Message = $"強震モニタからのデータ取得でHTTPエラーが発生しました{(details != null ? $": {details}" : "")}"
		};

	public static ErrorPayload ParseError(DateTime time, string? details = null)
		=> new()
		{
			ErrorType = "parse_error",
			Time = time,
			Message = $"強震モニタのデータ解析でエラーが発生しました{(details != null ? $": {details}" : "")}"
		};
}

/// <summary>
/// 領域ペイロード
/// </summary>
public record RegionPayload
{
	public required LocationPayload TopLeft { get; init; }
	public required LocationPayload BottomRight { get; init; }
}

/// <summary>
/// 座標ペイロード
/// </summary>
public record LocationPayload
{
	public required float Latitude { get; init; }
	public required float Longitude { get; init; }
}

/// <summary>
/// 観測点ペイロード
/// </summary>
public record ObservationPointPayload
{
	public required string Code { get; init; }
	public required string Name { get; init; }
	public required string Region { get; init; }
	public required string Type { get; init; }
	public required LocationPayload Location { get; init; }
	public required double? Intensity { get; init; }
	public required double IntensityDiff { get; init; }
}
