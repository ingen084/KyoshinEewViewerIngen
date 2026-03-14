using KyoshinEewViewer.Core;
using KyoshinMonitorLib;
using System;

namespace KyoshinEewViewer.Series.Earthquake.Models;

/// <summary>
/// 地震情報の中間表現
/// JMA XML, AXIS JSON, P2P地震情報 などのソースから変換される共通構造
/// </summary>
public class EarthquakeInformationData
{
	/// <summary>
	/// 電文タイトル (震度速報, 震源に関する情報, 震源・震度に関する情報, 長周期地震動に関する観測情報, 顕著な地震の震源要素更新のお知らせ)
	/// </summary>
	public required string Title { get; init; }

	/// <summary>
	/// イベントID
	/// </summary>
	public required string EventId { get; init; }

	/// <summary>
	/// 情報種別
	/// </summary>
	public required EarthquakeInfoType InfoType { get; init; }

	/// <summary>
	/// 電文ステータス
	/// </summary>
	public required EarthquakeReportStatus Status { get; init; }

	/// <summary>
	/// 発表日時
	/// </summary>
	public required DateTime ReportDateTime { get; init; }

	/// <summary>
	/// 情報ソース識別
	/// </summary>
	public required string Source { get; init; }

	/// <summary>
	/// 電文の重複判定キー (JMA XMLの場合はTelegram.Key、外部APIの場合はnullで自動生成)
	/// </summary>
	public string? TelegramKey { get; init; }

	/// <summary>
	/// 震源情報 (震度速報にはない)
	/// </summary>
	public EarthquakeHypocenterData? Hypocenter { get; init; }

	/// <summary>
	/// 震度情報 (震源に関する情報のみにはない)
	/// </summary>
	public EarthquakeIntensityData? Intensity { get; init; }

	/// <summary>
	/// 固定付加文
	/// </summary>
	public string? ForecastComment { get; init; }

	/// <summary>
	/// 自由形式文
	/// </summary>
	public string? FreeFormComment { get; init; }

	/// <summary>
	/// 海外で発生した地震か
	/// </summary>
	public bool IsForeign { get; init; }

	/// <summary>
	/// 大規模な噴火か
	/// </summary>
	public bool IsVolcano { get; init; }

	/// <summary>
	/// 噴火の場合の火山名
	/// </summary>
	public string? VolcanoName { get; init; }

	/// <summary>
	/// 最大の長周期地震動階級
	/// </summary>
	public LpgmIntensity? MaxLpgmIntensity { get; init; }
}

/// <summary>
/// 情報種別
/// </summary>
public enum EarthquakeInfoType
{
	/// <summary>
	/// 発表
	/// </summary>
	Normal,

	/// <summary>
	/// 取消
	/// </summary>
	Cancel,

	/// <summary>
	/// 訂正
	/// </summary>
	Correction,
}

/// <summary>
/// 電文ステータス
/// </summary>
public enum EarthquakeReportStatus
{
	/// <summary>
	/// 通常
	/// </summary>
	Normal,

	/// <summary>
	/// 訓練
	/// </summary>
	Training,

	/// <summary>
	/// 試験
	/// </summary>
	Test,
}

/// <summary>
/// 震源情報
/// </summary>
public class EarthquakeHypocenterData
{
	/// <summary>
	/// 発生時刻
	/// </summary>
	public required DateTime OccurrenceTime { get; init; }

	/// <summary>
	/// 震央地名
	/// </summary>
	public required string Place { get; init; }

	/// <summary>
	/// 震央座標 (P2Pソースでは座標なしの場合あり)
	/// </summary>
	public Location? Location { get; init; }

	/// <summary>
	/// マグニチュード
	/// </summary>
	public required float Magnitude { get; init; }

	/// <summary>
	/// マグニチュードの代替テキスト (「不明」など)
	/// </summary>
	public string? MagnitudeAlternativeText { get; init; }

	/// <summary>
	/// 深さ(km) (-1 = 不明, 0 = ごく浅い)
	/// </summary>
	public required int Depth { get; init; }
}

/// <summary>
/// 震度情報
/// </summary>
public class EarthquakeIntensityData
{
	/// <summary>
	/// 最大震度
	/// </summary>
	public required JmaIntensity MaxIntensity { get; init; }

	/// <summary>
	/// 検知時刻 (震度速報の場合)
	/// </summary>
	public DateTime? DetectionTime { get; init; }

	/// <summary>
	/// 代表地域名 (震度速報の場合、最大震度の地域)
	/// </summary>
	public string? RepresentativeAreaName { get; init; }

	/// <summary>
	/// 発表地域が1つのみか (震度速報の場合)
	/// </summary>
	public bool IsOnlyArea { get; init; }
}

/// <summary>
/// 都道府県の観測情報
/// </summary>
public class EarthquakeObservationPref
{
	/// <summary>
	/// 都道府県名
	/// </summary>
	public required string Name { get; init; }

	/// <summary>
	/// 都道府県コード
	/// </summary>
	public required int Code { get; init; }

	/// <summary>
	/// 地域の観測情報
	/// </summary>
	public EarthquakeObservationArea[] Areas { get; init; } = [];
}

/// <summary>
/// 細分区域の観測情報
/// </summary>
public class EarthquakeObservationArea
{
	/// <summary>
	/// 地域名
	/// </summary>
	public required string Name { get; init; }

	/// <summary>
	/// 地域コード
	/// </summary>
	public required int Code { get; init; }

	/// <summary>
	/// 最大震度
	/// </summary>
	public required JmaIntensity MaxIntensity { get; init; }

	/// <summary>
	/// 市区町村の観測情報
	/// </summary>
	public EarthquakeObservationCity[] Cities { get; init; } = [];
}

/// <summary>
/// 市区町村の観測情報
/// </summary>
public class EarthquakeObservationCity
{
	/// <summary>
	/// 市区町村名
	/// </summary>
	public required string Name { get; init; }

	/// <summary>
	/// 市区町村コード
	/// </summary>
	public required int Code { get; init; }

	/// <summary>
	/// 最大震度
	/// </summary>
	public required JmaIntensity MaxIntensity { get; init; }

	/// <summary>
	/// 観測点の情報
	/// </summary>
	public EarthquakeObservationStation[] Stations { get; init; } = [];
}

/// <summary>
/// 観測点の情報
/// </summary>
public class EarthquakeObservationStation
{
	/// <summary>
	/// 観測点名
	/// </summary>
	public required string Name { get; init; }

	/// <summary>
	/// 観測点コード
	/// </summary>
	public required string Code { get; init; }

	/// <summary>
	/// 震度
	/// </summary>
	public required JmaIntensity Intensity { get; init; }
}

/// <summary>
/// フラット構造の震度観測点 (P2P用: 座標・コードなし)
/// </summary>
public class EarthquakeObservationFlatPoint
{
	/// <summary>
	/// 都道府県名
	/// </summary>
	public required string PrefName { get; init; }

	/// <summary>
	/// 観測点名称または区域名
	/// </summary>
	public required string Address { get; init; }

	/// <summary>
	/// 震度
	/// </summary>
	public required JmaIntensity Intensity { get; init; }

	/// <summary>
	/// 区域名かどうか (trueの場合は地域名、falseの場合は観測点名)
	/// </summary>
	public required bool IsArea { get; init; }
}
