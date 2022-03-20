using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Headline;

public class HeadlineInformation
{
	private XmlNode Node { get; set; }

	public HeadlineInformation(XmlNode node)
	{
		Node = node;
	}

	private string? type;
	/// <summary>
	/// 事項種別
	/// </summary>
	public string Type => type ??= (Node.TryFindStringAttribute(Literals.AttrType(), out var n) ? n : throw new JmaXmlParseException("type 属性が存在しません"));

	/// <summary>
	/// 事項種別と対象地域
	/// </summary>
	public IEnumerable<HeadlineInformationItem> Items
	{
		get {
			foreach (var info in Node.Children.Where(c => c.Name == Literals.Item()))
				yield return new(info);
		}
	}
}
