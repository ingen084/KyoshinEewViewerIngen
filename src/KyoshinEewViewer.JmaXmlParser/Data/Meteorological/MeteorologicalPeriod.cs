using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;

/// <summary>
/// 警戒・注意期間。文字列表現をそのまま保持する。
/// </summary>
public struct MeteorologicalPeriod(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	/// <summary>
	/// 開始時間帯。存在しない場合は null。
	/// </summary>
	public ForecastTerm? StartTime => Node.TryFindChild("StartTime"u8, out var n) ? new(n) : null;

	/// <summary>
	/// 終了時間帯。存在しない場合は null。
	/// </summary>
	public ForecastTerm? EndTime => Node.TryFindChild("EndTime"u8, out var n) ? new(n) : null;

	/// <summary>
	/// 終了時間帯以降も続くことの補足。存在しない場合は null。
	/// </summary>
	public string? OverTime => Node.TryFindStringNode("OverTime"u8, out var n) ? n : null;

	/// <summary>
	/// 対象時間帯。存在しない場合は null。
	/// </summary>
	public ForecastTerm? ZoneTime => Node.TryFindChild("ZoneTime"u8, out var n) ? new(n) : null;
}
