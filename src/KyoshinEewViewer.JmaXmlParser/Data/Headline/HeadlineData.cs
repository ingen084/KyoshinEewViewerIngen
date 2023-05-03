using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Headline;

public struct HeadlineData
{
	private XmlNode Node { get; set; }

	internal HeadlineData(XmlNode node)
	{
		Node = node;
	}

	private string? _text = null;
	/// <summary>
	/// 電文の内容を簡潔に伝える文章 空の場合もある
	/// </summary>
	public string Text => _text ??= (Node.TryFindStringNode(Literals.Text(), out var n) ? n : throw new JmaXmlParseException("Text ノードが存在しません"));

	/// <summary>
	/// 事項種別と対象地域
	/// <para>(地震火山) <seealso href="https://dmdata.jp/docs/jma/manual/0101-0183.pdf#page=10"/></para>
	/// </summary>
	public IEnumerable<HeadlineInformation> Informations
		=> Node.Children.Where(c => c.Name == Literals.Information()).Select(c => new HeadlineInformation(c));
}
