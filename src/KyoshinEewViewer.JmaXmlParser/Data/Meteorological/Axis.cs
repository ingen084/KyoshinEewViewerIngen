using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;
public struct Axis(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	private PhysicalQuantity? _direction = null;
	/// <summary>
	/// 予報円の半径の方向<br/>
	/// condition および description の値は “全域”
	/// </summary>
	public PhysicalQuantity Direction => _direction ??= (Node.TryFindElementBasis("Direction"u8, out var c) ? new(c) : throw new JmaXmlParseException("Direction ノードが存在しません"));

	/// <summary>
	/// 半径の方向の全件。
	/// </summary>
	public IEnumerable<PhysicalQuantity> Directions
		=> Node.Children.Where(c => c.IsElementBasis("Direction"u8)).Select(c => new PhysicalQuantity(c));

	/// <summary>
	/// 半径の方位角。
	/// </summary>
	public IEnumerable<PhysicalQuantity> Bearings
		=> Node.Children.Where(c => c.IsElementBasis("Bearings"u8)).Select(c => new PhysicalQuantity(c));

	/// <summary>
	/// 予報円/暴風域/強風域の半径
	/// </summary>
	public IEnumerable<PhysicalQuantity> Radiuses
		=> Node.Children.Where(c => c.IsElementBasis("Radius"u8)).Select(c => new PhysicalQuantity(c));
}
