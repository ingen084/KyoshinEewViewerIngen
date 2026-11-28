using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

/// <summary>
/// 地震関連情報の本文と情報種別。
/// </summary>
public struct EarthquakeInfo(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	private string? _type = null;
	/// <summary>
	/// 地震関連情報の種別。旧形式の Tokai 要素の場合は null。
	/// </summary>
	public string? Type => _type ??= (Node.TryFindStringAttribute(Literals.AttrType(), out var n) ? n :
		Node.Name == "Tokai"u8 ? null : throw new JmaXmlParseException("type 属性が存在しません"));

	private string? _infoKind = null;
	/// <summary>
	/// 情報名称。
	/// </summary>
	public string InfoKind => _infoKind ??= (Node.TryFindStringNode(Literals.InfoKind(), out var n) ? n : throw new JmaXmlParseException("InfoKind ノードが存在しません"));

	private InfoSerial? _infoSerial = null;
	/// <summary>
	/// 情報種別番号。存在しない場合は null。
	/// </summary>
	public InfoSerial? InfoSerial => _infoSerial ??= (Node.TryFindChild("InfoSerial"u8, out var n) ? new(n) : null);

	private string? _text = null;
	/// <summary>
	/// 情報の本文。
	/// </summary>
	public string Text => _text ??= (Node.TryFindStringNode(Literals.Text(), out var n) ? n : throw new JmaXmlParseException("Text ノードが存在しません"));

	private string? _appendix = null;
	/// <summary>
	/// 参考情報。存在しない場合は null。
	/// </summary>
	public string? Appendix => _appendix ??= (Node.TryFindStringNode(Literals.Appendix(), out var n) ? n : null);
}
