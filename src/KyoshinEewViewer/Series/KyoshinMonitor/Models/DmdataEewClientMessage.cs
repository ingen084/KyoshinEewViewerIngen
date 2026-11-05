using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Models;

/// <summary>
/// EEW Client by DMDATA.JP の外部連携機能から WebSocket で配信されるメッセージ
/// type によって使用されるプロパティが異なる
/// </summary>
public class DmdataEewClientMessage
{
	/// <summary>
	/// メッセージ種別 start / user-point / server-status / eew
	/// </summary>
	public string? Type { get; set; }

	/// <summary>
	/// start: EEW Client のバージョン
	/// </summary>
	public string? Version { get; set; }

	/// <summary>
	/// user-point: 予想地点の座標 [経度, 緯度]
	/// 未設定の場合は null、共有しない設定の場合は false が送られてくるため JsonElement で受ける
	/// </summary>
	public JsonElement? Location { get; set; }

	/// <summary>
	/// server-status: EEW Client と dmdata.jp の接続状況
	/// no-auth / no-contract / limit / close / connecting / open
	/// </summary>
	public string? Status { get; set; }

	public string? EventId { get; set; }
	public int Serial { get; set; }
	/// <summary>
	/// 発表官署
	/// </summary>
	public string? Author { get; set; }
	public DateTimeOffset? PressTime { get; set; }
	public DateTimeOffset? ProcessTime { get; set; }
	public bool IsCanceled { get; set; }
	public bool IsLastInfo { get; set; }
	public bool IsPlumOnly { get; set; }
	public bool IsLevelOver { get; set; }
	public bool IsWarning { get; set; }

	public string? EpicenterName { get; set; }
	/// <summary>
	/// 震央座標 [経度, 緯度]
	/// </summary>
	public double[]? EpicenterLocation { get; set; }
	public int? Depth { get; set; }
	/// <summary>
	/// マグニチュード PLUM法のみ・レベル法の場合は null
	/// </summary>
	public double? Magnitude { get; set; }
	public DateTimeOffset? OriginTime { get; set; }
	public DateTimeOffset? ArrivalTime { get; set; }
	public DmdataEewClientAccuracy? Accuracy { get; set; }

	public DmdataEewClientIntensityRange? MaxInt { get; set; }
	public DmdataEewClientIntensityRange? MaxLgInt { get; set; }

	/// <summary>
	/// 細分区域ごとの予測震度
	/// </summary>
	public DmdataEewClientRegion[]? Regions { get; set; }
	/// <summary>
	/// 細分区域ごとの予測震度 (ドキュメント上の名称)
	/// </summary>
	public DmdataEewClientRegion[]? RegionForecasts { get; set; }

	/// <summary>
	/// EEW Client に設定された予想地点の予測 未設定の場合は null
	/// </summary>
	public DmdataEewClientPointForecast? PointForecast { get; set; }
}

public class DmdataEewClientAccuracy
{
	/// <summary>
	/// [震央位置の精度, 震源位置の精度]
	/// </summary>
	public int[]? Epicenters { get; set; }
	public int? Depth { get; set; }
	public int? MagnitudeCalculation { get; set; }
	public int? NumberOfMagnitudeCalculation { get; set; }
}

public class DmdataEewClientIntensityRange
{
	public string? From { get; set; }
	/// <summary>
	/// 上限 "over" の場合は以上を表す
	/// </summary>
	public string? To { get; set; }
}

public class DmdataEewClientRegion
{
	public int Code { get; set; }
	/// <summary>
	/// 予測震度 (実際の配信で使用されている名称)
	/// </summary>
	public string? MaxInt { get; set; }
	/// <summary>
	/// 予測震度 (ドキュメント上の名称)
	/// </summary>
	public string? To { get; set; }
}

public class DmdataEewClientPointForecast
{
	public DmdataEewClientSWave? SWave { get; set; }
	public DmdataEewClientLpgm? Lpgm { get; set; }
	public DmdataEewClientPointIntensity? Intensity { get; set; }
}

public class DmdataEewClientSWave
{
	/// <summary>
	/// ok / not-calculated
	/// </summary>
	public string? Status { get; set; }
	public DateTimeOffset? Time { get; set; }
}

public class DmdataEewClientLpgm
{
	public double? MaxSva { get; set; }
	public string? LgInt { get; set; }
}

public class DmdataEewClientPointIntensity
{
	/// <summary>
	/// attenuation (従来法) / plum (PLUM法)
	/// </summary>
	public string? Type { get; set; }
	/// <summary>
	/// 計測震度
	/// </summary>
	public double? K { get; set; }
	/// <summary>
	/// 震度階級
	/// </summary>
	public string? Int { get; set; }
}

// トリミング時にプロパティが削除されないよう、ソース生成のコンテキストを使用する
// 数値は文字列で送られてくる項目があるため、文字列からの読み取りを許可する
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(DmdataEewClientMessage))]
public partial class DmdataEewClientSerializerContext : JsonSerializerContext;
