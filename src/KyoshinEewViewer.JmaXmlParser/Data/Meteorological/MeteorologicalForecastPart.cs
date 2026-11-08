using System;
using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;

/// <summary>
/// 気象警報・注意報の危険度と量的予想を構成する共通部品。Base、Local などの階層を保持する。
/// </summary>
public struct MeteorologicalForecastPart(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	/// <summary>
	/// 部品または部分予想の要素名。
	/// </summary>
	public string Name => Node.Name.ToString();

	/// <summary>
	/// 時刻の参照番号。存在しない場合は null。
	/// </summary>
	public int? RefId => Node.TryFindIntAttribute(Literals.AttrRefId(), out var n) ? n : null;

	/// <summary>
	/// 基本となる予想。存在しない場合は null。
	/// </summary>
	public MeteorologicalForecastPart? Base => Node.TryFindChild("Base"u8, out var n) ? new(n) : null;

	/// <summary>
	/// 一時的な変化。
	/// </summary>
	public IEnumerable<MeteorologicalForecastPart> Temporaries
		=> Node.Children.Where(c => c.Name == "Temporary"u8).Select(c => new MeteorologicalForecastPart(c));

	/// <summary>
	/// 移行後の予想。
	/// </summary>
	public IEnumerable<MeteorologicalForecastPart> Becomings
		=> Node.Children.Where(c => c.Name == "Becoming"u8).Select(c => new MeteorologicalForecastPart(c));

	/// <summary>
	/// 地域ごとの予想。
	/// </summary>
	public IEnumerable<MeteorologicalForecastPart> SubAreas
		=> Node.Children.Where(c => c.Name == "SubArea"u8).Select(c => new MeteorologicalForecastPart(c));

	/// <summary>
	/// 陸上・海上・山地・海岸などの局地的な予想。
	/// </summary>
	public IEnumerable<MeteorologicalForecastPart> Locals
		=> Node.Children.Where(c => c.Name == "Local"u8).Select(c => new MeteorologicalForecastPart(c));

	/// <summary>
	/// 高潮などの時間順の予想。
	/// </summary>
	public IEnumerable<MeteorologicalForecastPart> Sequences
		=> Node.Children.Where(c => c.Name == "Sequence"u8).Select(c => new MeteorologicalForecastPart(c));

	/// <summary>
	/// 雨量・風速・波高・潮位・降雪量・湿度・視程などの基本要素。RefId で時刻定義と対応付ける。
	/// </summary>
	public IEnumerable<PhysicalQuantity> Quantities => Node.Children
		.Where(c => c.TryGetFullName(out var ns, out _) && ns == "http://xml.kishou.go.jp/jmaxml1/elementBasis1/"u8)
		.Select(c => new PhysicalQuantity(c));

	/// <summary>
	/// 危険度の分類。
	/// </summary>
	public IEnumerable<MeteorologicalClass> Significancies
		=> Node.Children.Where(c => c.Name == "Significancy"u8).Select(c => new MeteorologicalClass(c));

	/// <summary>
	/// 風速の階級。
	/// </summary>
	public IEnumerable<WindSpeedLevel> WindSpeedLevels
		=> Node.Children.Where(c => c.Name == "WindSpeedLevel"u8).Select(c => new WindSpeedLevel(c));

	/// <summary>
	/// 基準到達クラス。存在しない場合は null。
	/// </summary>
	public MeteorologicalClass? CriteriaClass => Node.TryFindChild("CriteriaClass"u8, out var n) ? new(n) : null;

	/// <summary>
	/// 局地的な予想の対象地域。存在しない場合は null。
	/// </summary>
	public string? AreaName => Node.TryFindStringNode("AreaName"u8, out var n) ? n : null;

	/// <summary>
	/// 文章で示した予想。存在しない場合は null。
	/// </summary>
	public string? Sentence => Node.TryFindStringNode("Sentence"u8, out var n) ? n : null;

	/// <summary>
	/// 文章の種類。存在しない場合は null。
	/// </summary>
	public string? SentenceType => Node.TryFindChild("Sentence"u8, out var n) && n.TryFindStringAttribute(Literals.AttrType(), out var value) ? value : null;

	/// <summary>
	/// 予想の対象時刻や基準到達時刻。存在しない場合は null。
	/// </summary>
	public DateTimeOffset? Time => Node.TryFindDateTimeNode("Time"u8, out var n) ? n : null;

	/// <summary>
	/// 時刻の種類。存在しない場合は null。
	/// </summary>
	public string? TimeType => Node.TryFindChild("Time"u8, out var n) && n.TryFindStringAttribute(Literals.AttrType(), out var value) ? value : null;

	/// <summary>
	/// 時間に関する補足。存在しない場合は null。
	/// </summary>
	public string? TimeModifier => Node.TryFindStringNode("TimeModifier"u8, out var n) ? n : null;

	/// <summary>
	/// 基準到達期間など（ISO 8601 の期間表現）。存在しない場合は null。
	/// </summary>
	public string? Duration => Node.TryFindStringNode("Duration"u8, out var n) ? n : null;

	/// <summary>
	/// 期間の種類。存在しない場合は null。
	/// </summary>
	public string? DurationType => Node.TryFindChild("Duration"u8, out var n) && n.TryFindStringAttribute(Literals.AttrType(), out var value) ? value : null;

	/// <summary>
	/// 現象のピークの時間帯。存在しない場合は null。
	/// </summary>
	public ForecastTerm? PeakTime => Node.TryFindChild("PeakTime"u8, out var n) ? new(n) : null;

	/// <summary>
	/// 補足情報。存在しない場合は null。
	/// </summary>
	public string? Remark => Node.TryFindStringNode("Remark"u8, out var n) ? n : null;

	/// <summary>
	/// 特記事項。
	/// </summary>
	public IEnumerable<string> AttentionNotes => Node.GetNotes("Attention"u8);

	/// <summary>
	/// 付加事項。
	/// </summary>
	public IEnumerable<string> AdditionNotes => Node.GetNotes("Addition"u8);
}
