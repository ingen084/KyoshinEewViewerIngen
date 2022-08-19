using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data;
public struct Category
{
	private XmlNode Node { get; set; }

	public Category(XmlNode node)
	{
		Node = node;
	}

	private Kind? kind = null;
	/// <summary>
	/// 事項種別
	/// </summary>
	public Kind Kind => kind ??= (Node.TryFindChild(Literals.Kind(), out var c) ? new(c) : throw new JmaXmlParseException("Kind ノードが存在しません"));

	private Kind? lastKind = null;
	/// <summary>
	/// 事項種別
	/// </summary>
	public Kind LastKind => lastKind ??= (Node.TryFindChild(Literals.LastKind(), out var c) ? new(c) : throw new JmaXmlParseException("LastKind ノードが存在しません"));
}
