using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

/// <summary>
/// 地震関連情報の情報種別番号。
/// </summary>
public struct InfoSerial(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	private string? _codeType = null;
	/// <summary>
	/// コード種別。
	/// </summary>
	public string CodeType => _codeType ??= (Node.TryFindStringAttribute(Literals.AttrCodeType(), out var n) ? n : throw new JmaXmlParseException("codeType 属性が存在しません"));

	private string? _name = null;
	/// <summary>
	/// 情報種別番号名。
	/// </summary>
	public string Name => _name ??= (Node.TryFindStringNode(Literals.Name(), out var n) ? n : throw new JmaXmlParseException("Name ノードが存在しません"));

	private string? _code = null;
	/// <summary>
	/// 情報種別番号コード。
	/// </summary>
	public string Code => _code ??= (Node.TryFindStringNode(Literals.Code(), out var n) ? n : throw new JmaXmlParseException("Code ノードが存在しません"));
}
