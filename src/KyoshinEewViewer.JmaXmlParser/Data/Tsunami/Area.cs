using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Tsunami;
public struct Area
{
	private XmlNode Node { get; set; }

	public Area(XmlNode node)
	{
		Node = node;
	}

	private string? _name = null;
	/// <summary>
	/// 地域名
	/// </summary>
	public string Name => _name ??= (Node.TryFindStringNode(Literals.Name(), out var n) ? n : throw new JmaXmlParseException("Name ノードが存在しません"));

	private int? _code = null;
	/// <summary>
	/// 地域コード
	/// </summary>
	public int Code => _code ??= (Node.TryFindIntNode(Literals.Code(), out var n) ? n : throw new JmaXmlParseException("Code ノードが存在しません"));
}
