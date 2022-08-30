using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;

public struct WindPart
{
	private XmlNode Node { get; set; }

	public WindPart(XmlNode node)
	{
		Node = node;
	}

	/// <summary>
	/// 予報円の中心位置<br/>
	/// type が 最大風速 の場合、 condition は、値が "中心付近"、"中心付近を除く"、"なし" の場合がある
	/// </summary>
	public IEnumerable<PhysicalQuantity> WindSpeeds
		=> Node.Children.Where(c => c.Name == Literals.JmaEbWindSpeed()).Select(c => new PhysicalQuantity(c));
}
