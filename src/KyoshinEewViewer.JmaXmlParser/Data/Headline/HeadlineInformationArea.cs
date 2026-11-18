using System.Collections.Generic;
using System.Linq;
using KyoshinEewViewer.JmaXmlParser.Data.Meteorological;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Headline;

public struct HeadlineInformationArea(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	private string? _name = null;
	/// <summary>
	/// 対象地域名
	/// </summary>
	public string Name => _name ??= (Node.TryFindStringNode(Literals.Name(), out var n) ? n : throw new JmaXmlParseException("Name ノードが存在しません"));

	private string? _code = null;
	/// <summary>
	/// 対象地域コード。座標などで領域を示す場合、省略されると null。
	/// </summary>
	public string? Code => _code ??= (Node.TryFindStringNode(Literals.Code(), out var n) ? n : null);

	/// <summary>
	/// 対象地域を表す点の座標。
	/// </summary>
	public IEnumerable<Coordinate> Coordinates
		=> Node.Children.Where(c => c.IsElementBasis("Coordinate"u8)).Select(c => new Coordinate(c));

	/// <summary>
	/// 対象地域を表す円。
	/// </summary>
	public IEnumerable<TyphoonCircle> Circles
		=> Node.Children.Where(c => c.IsElementBasis("Circle"u8)).Select(c => new TyphoonCircle(c));

	/// <summary>
	/// 対象地域を表す線の座標列。
	/// </summary>
	public IEnumerable<Coordinate> Lines
		=> Node.Children.Where(c => c.IsElementBasis("Line"u8)).Select(c => new Coordinate(c));

	/// <summary>
	/// 対象地域を表す多角形の座標列。
	/// </summary>
	public IEnumerable<Coordinate> Polygons
		=> Node.Children.Where(c => c.IsElementBasis("Polygon"u8)).Select(c => new Coordinate(c));
}
