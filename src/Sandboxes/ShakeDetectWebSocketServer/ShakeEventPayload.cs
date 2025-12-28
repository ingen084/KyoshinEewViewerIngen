using System;
using System.Linq;
using System.Text.Json.Serialization;
using KyoshinEewViewer.Core.Models;

namespace ShakeDetectWebSocketServer;

/// <summary>
/// WebSocketで送信するペイロードの基底クラス
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ShakeDetectedPayload), "shake_detected")]
[JsonDerivedType(typeof(ErrorPayload), "error")]
public abstract record WebSocketPayload;

/// <summary>
/// 揺れ検知イベントペイロード
/// </summary>
public record ShakeDetectedPayload : WebSocketPayload
{
	public required Guid EventId { get; init; }
	public required DateTime CreatedAt { get; init; }
	public required string Level { get; init; }
	public required int LevelValue { get; init; }
	public required bool IsLevelUp { get; init; }
	public required bool IsReplay { get; init; }
	public required int PointCount { get; init; }
	public required RegionPayload Region { get; init; }
	public required ObservationPointPayload[] Points { get; init; }

	public static ShakeDetectedPayload FromEvent(KyoshinEvent evt, bool isLevelUp, bool isReplay)
	{
		return new ShakeDetectedPayload
		{
			EventId = evt.Id,
			CreatedAt = evt.CreatedAt,
			Level = evt.Level.ToString(),
			LevelValue = (int)evt.Level,
			IsLevelUp = isLevelUp,
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
}

/// <summary>
/// エラーペイロード
/// </summary>
public record ErrorPayload : WebSocketPayload
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
