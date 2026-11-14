using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;

public struct TyphoonCircle(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	private string? _type = null;
	/// <summary>
	/// 風域の種類<br/>
	/// <list type="bullet">
	/// <item>強風域</item>
	/// <item>予報円</item>
	/// </list>
	/// 存在しない場合は null。
	/// </summary>
	public string? Type => _type ??= (Node.TryFindStringAttribute(Literals.AttrType(), out var n) ? n : null);

	/// <summary>
	/// 強風域及び予報円の中心位置
	/// </summary>
	public IEnumerable<Coordinate> BasePoints
		=> Node.Children.Where(c => c.IsElementBasis("BasePoint"u8)).Select(c => new Coordinate(c));

	/// <summary>
	/// 強風域･暴風域の距離情報。Axes 要素が存在しない場合は空。
	/// </summary>
	public IEnumerable<Axis> Axes
	{
		get {
			if (!Node.TryFindElementBasis("Axes"u8, out var c))
				return Enumerable.Empty<Axis>();
			return c.Children.Where(c => c.IsElementBasis("Axis"u8)).Select(c => new Axis(c));
		}
	}

	/// <summary>
	/// 楕円の長軸。存在しない場合は空。
	/// </summary>
	public IEnumerable<Axis> LongAxes => Node.TryFindElementBasis("Axes"u8, out var n)
		? n.Children.Where(c => c.IsElementBasis("LongAxis"u8)).Select(c => new Axis(c))
		: Enumerable.Empty<Axis>();

	/// <summary>
	/// 楕円の短軸。存在しない場合は空。
	/// </summary>
	public IEnumerable<Axis> ShortAxes => Node.TryFindElementBasis("Axes"u8, out var n)
		? n.Children.Where(c => c.IsElementBasis("ShortAxis"u8)).Select(c => new Axis(c))
		: Enumerable.Empty<Axis>();
}
