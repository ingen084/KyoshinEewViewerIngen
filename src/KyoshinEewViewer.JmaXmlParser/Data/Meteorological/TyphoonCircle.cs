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
	/// </summary>
	public string Type => _type ??= (Node.TryFindStringAttribute(Literals.AttrType(), out var n) ? n : throw new JmaXmlParseException("type 属性が存在しません"));

	/// <summary>
	/// 強風域及び予報円の中心位置
	/// </summary>
	public IEnumerable<Coordinate> BasePoints
		=> Node.Children.Where(c => c.Name == Literals.JmaEbBasePoint()).Select(c => new Coordinate(c));

	/// <summary>
	/// 強風域･暴風域の距離情報
	/// </summary>
	public IEnumerable<Axis> Axes
	{
		get {
			if (!Node.TryFindChild(Literals.JmaEbAxes(), out var c))
				throw new JmaXmlParseException("Axes ノードが存在しません");
			return c.Children.Where(c => c.Name == Literals.JmaEbAxis()).Select(c => new Axis(c));
		}
	}
}
