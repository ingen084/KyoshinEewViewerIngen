using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Tsunami;

/// <summary>
/// 津波予報の対象市町村。
/// </summary>
public struct TsunamiCity(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	private string? _name = null;
	/// <summary>
	/// 市町村名。
	/// </summary>
	public string Name => _name ??= (Node.TryFindStringNode(Literals.Name(), out var n) ? n : throw new JmaXmlParseException("Name ノードが存在しません"));

	private string? _code = null;
	/// <summary>
	/// 市町村コード。
	/// </summary>
	public string Code => _code ??= (Node.TryFindStringNode(Literals.Code(), out var n) ? n : throw new JmaXmlParseException("Code ノードが存在しません"));
}
