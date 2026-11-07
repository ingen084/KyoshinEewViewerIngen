using System;
using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;
public struct CenterPart(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	private DateTimeOffset? _time = null;
	/// <summary>
	/// 中心情報の対象時刻。存在しない場合は null。
	/// </summary>
	public DateTimeOffset? Time => _time ??= (Node.TryFindDateTimeNode("Time"u8, out var n) ? n : null);

	private string? _remark = null;
	/// <summary>
	/// 中心情報についての補足情報。存在しない場合は null。
	/// </summary>
	public string? Remark => _remark ??= (Node.TryFindStringNode(Literals.Remark(), out var n) ? n : null);

	/// <summary>
	/// 予報円の全件。
	/// </summary>
	public IEnumerable<TyphoonCircle> ProbabilityCircles
		=> Node.Children.Where(c => c.Name == Literals.ProbabilityCircle()).Select(c => new TyphoonCircle(c));

	/// <summary>
	/// 移動方向の全件。
	/// </summary>
	public IEnumerable<PhysicalQuantity> Directions
		=> Node.Children.Where(c => c.IsElementBasis("Direction"u8)).Select(c => new PhysicalQuantity(c));

	/// <summary>
	/// 中心からの半径。
	/// </summary>
	public IEnumerable<PhysicalQuantity> Radiuses
		=> Node.Children.Where(c => c.IsElementBasis("Radius"u8)).Select(c => new PhysicalQuantity(c));

	/// <summary>
	/// 台風の中心位置<br/>
	/// 「実況」及び「推定 １時間後」の場合のみ出現
	/// </summary>
	public IEnumerable<Coordinate> Coordinates
		=> Node.Children.Where(c => c.IsElementBasis("Coordinate"u8)).Select(c => new Coordinate(c));

	private TyphoonCircle? _probabilityCircle = null;
	/// <summary>
	/// 予報円<br/>
	/// 「予報 X 時間後」及び「延長予報 X 時間後」の場合のみ出現
	/// </summary>
	public TyphoonCircle? ProbabilityCircle => _probabilityCircle ??= (Node.TryFindChild(Literals.ProbabilityCircle(), out var n) ? new(n) : null);

	private string? _location = null;
	/// <summary>
	/// 台風の存在域<br/>
	/// 例: 与論島の西北西約 80km
	/// </summary>
	public string? Location => _location ??= (Node.TryFindStringNode(Literals.Location(), out var n) ? n : null);

	private PhysicalQuantity? _direction = null;
	/// <summary>
	/// 台風の移動方向<br/>
	/// 空の場合、 condition="不定"、description="不定" となる
	/// </summary>
	public PhysicalQuantity Direction => _direction ??= (Node.TryFindElementBasis("Direction"u8, out var n) ? new(n) : throw new JmaXmlParseException("Direction ノードが存在しません"));

	/// <summary>
	/// 台風の移動速度
	/// </summary>
	public IEnumerable<PhysicalQuantity> Speeds
		=> Node.Children.Where(c => c.IsElementBasis("Speed"u8)).Select(c => new PhysicalQuantity(c));

	private PhysicalQuantity? _pressure = null;
	/// <summary>
	/// 台風の中心気圧
	/// </summary>
	public PhysicalQuantity Pressure => _pressure ??= (Node.TryFindElementBasis("Pressure"u8, out var n) ? new(n) : throw new JmaXmlParseException("Pressure ノードが存在しません"));
}
