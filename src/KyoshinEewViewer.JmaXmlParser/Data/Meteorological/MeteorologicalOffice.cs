using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;

/// <summary>
/// 発表官署や照会先の情報。
/// </summary>
public struct MeteorologicalOffice(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	/// <summary>
	/// 官署情報の種類。
	/// </summary>
	public string Type => Node.TryFindStringAttribute("type"u8, out var n) ? n : throw new JmaXmlParseException("type 属性が存在しません");

	/// <summary>
	/// 官署名。
	/// </summary>
	public string Name => Node.TryFindStringNode("Name"u8, out var n) ? n : throw new JmaXmlParseException("Name ノードが存在しません");

	/// <summary>
	/// 官署コード。存在しない場合は null。
	/// </summary>
	public string? Code => Node.TryFindStringNode("Code"u8, out var n) ? n : null;

	/// <summary>
	/// 連絡先。存在しない場合は null。
	/// </summary>
	public string? ContactInfo => Node.TryFindStringNode("ContactInfo"u8, out var n) ? n : null;

	/// <summary>
	/// 参照先 URI。存在しない場合は null。
	/// </summary>
	public string? Uri => Node.TryFindStringNode("URI"u8, out var n) ? n : null;
}
