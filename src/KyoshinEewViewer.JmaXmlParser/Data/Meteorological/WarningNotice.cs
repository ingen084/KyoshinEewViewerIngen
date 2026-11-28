using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;

/// <summary>
/// 注意報から警報への切替予告。
/// </summary>
public struct WarningNotice(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	/// <summary>
	/// 警報へ切り替える可能性がある時間帯。
	/// </summary>
	public ForecastTerm StartTime => Node.TryFindChild("StartTime"u8, out var n) ? new(n) : throw new JmaXmlParseException("StartTime ノードが存在しません");

	/// <summary>
	/// 切替予告の内容。
	/// </summary>
	public string Note => Node.TryFindStringNode("Note"u8, out var n) ? n : throw new JmaXmlParseException("Note ノードが存在しません");
}
