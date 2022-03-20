using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Headline;

public class HeadlineInformationArea
{
	private XmlNode Node { get; set; }

	public HeadlineInformationArea(XmlNode node)
	{
		Node = node;
	}

	private string? name;
	/// <summary>
	/// 対象地域名
	/// </summary>
	public string Name => name ??= (Node.TryFindStringNode(Literals.Name(), out var n) ? n : throw new JmaXmlParseException("Name ノードが存在しません"));

	private string? code;
	/// <summary>
	/// 対象地域コード
	/// </summary>
	public string Code => code ??= (Node.TryFindStringNode(Literals.Code(), out var n) ? n : throw new JmaXmlParseException("Code ノードが存在しません"));
}
