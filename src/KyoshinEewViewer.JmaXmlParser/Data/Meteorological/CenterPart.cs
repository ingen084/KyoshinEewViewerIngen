using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;
public struct CenterPart
{
	private XmlNode Node { get; set; }

	public CenterPart(XmlNode node)
	{
		Node = node;
	}

	/// <summary>
	/// 台風の中心位置<br/>
	/// 「実況」及び「推定 １時間後」の場合のみ出現
	/// </summary>
	public IEnumerable<Coordinate> Coordinates
		=> Node.Children.Where(c => c.Name == Literals.JmxEbCoordinate()).Select(c => new Coordinate(c));

	private TyphoonCircle? probabilityCircle = null;
	/// <summary>
	/// 予報円<br/>
	/// 「予報 X 時間後」及び「延長予報 X 時間後」の場合のみ出現
	/// </summary>
	public TyphoonCircle? ProbabilityCircle => probabilityCircle ??= (Node.TryFindChild(Literals.ProbabilityCircle(), out var n) ? new(n) : null);

	private string? location = null;
	/// <summary>
	/// 台風の存在域<br/>
	/// 例: 与論島の西北西約 80km
	/// </summary>
	public string? Location => location ??= (Node.TryFindStringNode(Literals.Location(), out var n) ? n : null);

	private PhysicalQuantity? direction = null;
	/// <summary>
	/// 台風の移動方向<br/>
	/// 空の場合、 condition="不定"、description="不定" となる
	/// </summary>
	public PhysicalQuantity Direction => direction ??= (Node.TryFindChild(Literals.JmaEbDirection(), out var n) ? new(n) : throw new JmaXmlParseException("Direction ノードが存在しません"));

	/// <summary>
	/// 台風の移動速度
	/// </summary>
	public IEnumerable<PhysicalQuantity> Speeds
		=> Node.Children.Where(c => c.Name == Literals.JmaEbSpeed()).Select(c => new PhysicalQuantity(c));

	private PhysicalQuantity? pressure = null;
	/// <summary>
	/// 台風の中心気圧
	/// </summary>
	public PhysicalQuantity Pressure => pressure ??= (Node.TryFindChild(Literals.JmaEbPressure(), out var n) ? new(n) : throw new JmaXmlParseException("Pressure ノードが存在しません"));
}
