using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;
public struct Axis
{
	private XmlNode Node { get; set; }

	public Axis(XmlNode node)
	{
		Node = node;
	}

	private PhysicalQuantity? direction = null;
	/// <summary>
	/// 予報円の半径の方向<br/>
	/// condition および description の値は “全域”
	/// </summary>
	public PhysicalQuantity Direction => direction ??= (Node.TryFindChild(Literals.JmaEbDirection(), out var c) ? new(c) : throw new JmaXmlParseException("Direction ノードが存在しません"));

	/// <summary>
	/// 予報円/暴風域/強風域の半径
	/// </summary>
	public IEnumerable<PhysicalQuantity> Radiuses
		=> Node.Children.Where(c => c.Name == Literals.JmaEbRadius()).Select(c => new PhysicalQuantity(c));
}
