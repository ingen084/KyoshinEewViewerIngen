using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;

/// <summary>
/// 気象情報の種別付き解説文。
/// </summary>
public struct MeteorologicalText(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	/// <summary>
	/// 解説文の種類。
	/// </summary>
	public string Type => Node.TryFindStringAttribute("type"u8, out var n) ? n : throw new JmaXmlParseException("type 属性が存在しません");

	/// <summary>
	/// 本文。
	/// </summary>
	public string Value => Node.InnerText.ToString();
}
