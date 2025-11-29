using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Series.Ml.Models;

/// <summary>
/// データセットエクスポートのメタデータ
/// </summary>
public class DatasetMetadata
{
	/// <summary>
	/// フォーマットバージョン
	/// </summary>
	[JsonPropertyName("version")]
	public int Version { get; set; } = 1;

	/// <summary>
	/// 開始時刻（ISO 8601形式）
	/// </summary>
	[JsonPropertyName("startTime")]
	public string StartTime { get; set; } = "";

	/// <summary>
	/// 終了時刻（ISO 8601形式）
	/// </summary>
	[JsonPropertyName("endTime")]
	public string EndTime { get; set; } = "";

	/// <summary>
	/// 総フレーム数
	/// </summary>
	[JsonPropertyName("totalFrames")]
	public int TotalFrames { get; set; }

	/// <summary>
	/// 観測点数
	/// </summary>
	[JsonPropertyName("stationCount")]
	public int StationCount { get; set; }

	/// <summary>
	/// 観測点リスト
	/// </summary>
	[JsonPropertyName("stations")]
	public List<StationInfo> Stations { get; set; } = [];
}

/// <summary>
/// 観測点情報
/// </summary>
public class StationInfo
{
	/// <summary>
	/// 観測点コード
	/// </summary>
	[JsonPropertyName("code")]
	public string Code { get; set; } = "";

	/// <summary>
	/// 観測点名
	/// </summary>
	[JsonPropertyName("name")]
	public string Name { get; set; } = "";

	/// <summary>
	/// 地域名
	/// </summary>
	[JsonPropertyName("region")]
	public string Region { get; set; } = "";

	/// <summary>
	/// 緯度
	/// </summary>
	[JsonPropertyName("lat")]
	public float Lat { get; set; }

	/// <summary>
	/// 経度
	/// </summary>
	[JsonPropertyName("lon")]
	public float Lon { get; set; }
}

/// <summary>
/// 地震情報（エクスポート用）
/// </summary>
public class EarthquakeInfo
{
	/// <summary>
	/// 地震ID
	/// </summary>
	[JsonPropertyName("id")]
	public string Id { get; set; } = "";

	/// <summary>
	/// 発生時刻のフレームインデックス
	/// </summary>
	[JsonPropertyName("timeIndex")]
	public int TimeIndex { get; set; }

	/// <summary>
	/// 発生時刻（ISO 8601形式）
	/// </summary>
	[JsonPropertyName("time")]
	public string Time { get; set; } = "";

	/// <summary>
	/// 震央地名
	/// </summary>
	[JsonPropertyName("name")]
	public string? Name { get; set; }

	/// <summary>
	/// 緯度
	/// </summary>
	[JsonPropertyName("latitude")]
	public float? Latitude { get; set; }

	/// <summary>
	/// 経度
	/// </summary>
	[JsonPropertyName("longitude")]
	public float? Longitude { get; set; }

	/// <summary>
	/// 深さ（km）
	/// </summary>
	[JsonPropertyName("depth")]
	public int? Depth { get; set; }

	/// <summary>
	/// マグニチュード
	/// </summary>
	[JsonPropertyName("magnitude")]
	public float? Magnitude { get; set; }

	/// <summary>
	/// 最大震度（文字列）
	/// </summary>
	[JsonPropertyName("maxIntensity")]
	public string? MaxIntensity { get; set; }
}
