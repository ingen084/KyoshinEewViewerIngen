using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;

/// <summary>
/// 日付と時間帯を用いた予報時間の文字列表現。
/// </summary>
public struct ForecastTerm(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	/// <summary>
	/// 日付の文字列表現。存在しない場合は null。
	/// </summary>
	public string? Date => Node.TryFindStringNode("Date"u8, out var n) ? n : null;

	/// <summary>
	/// 時間帯の文字列表現。存在しない場合は null。
	/// </summary>
	public string? Term => Node.TryFindStringNode("Term"u8, out var n) ? n : null;
}
