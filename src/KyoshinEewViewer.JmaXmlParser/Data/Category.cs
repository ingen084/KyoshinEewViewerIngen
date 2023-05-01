using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data;
public struct Category
{
	private XmlNode Node { get; set; }

	public Category(XmlNode node)
	{
		Node = node;
	}

	private Kind? _kind = null;
	/// <summary>
	/// 事項種別
	/// </summary>
	public Kind Kind => _kind ??= (Node.TryFindChild(Literals.Kind(), out var c) ? new(c) : throw new JmaXmlParseException("Kind ノードが存在しません"));

	private Kind? _lastKind = null;
	/// <summary>
	/// 事項種別
	/// </summary>
	public Kind LastKind => _lastKind ??= (Node.TryFindChild(Literals.LastKind(), out var c) ? new(c) : throw new JmaXmlParseException("LastKind ノードが存在しません"));
}
