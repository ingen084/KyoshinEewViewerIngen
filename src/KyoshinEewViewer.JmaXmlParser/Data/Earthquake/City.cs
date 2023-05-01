using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

public struct City
{
	private XmlNode Node { get; set; }

	public City(XmlNode node)
	{
		Node = node;
	}

	private string? _name = null;
	/// <summary>
	/// 市町村名
	/// </summary>
	public string Name => _name ??= (Node.TryFindStringNode(Literals.Name(), out var n) ? n : throw new JmaXmlParseException("Name ノードが存在しません"));

	private int? _code = null;
	/// <summary>
	/// 市町村コード
	/// </summary>
	public int Code => _code ??= (Node.TryFindIntNode(Literals.Code(), out var n) ? n : throw new JmaXmlParseException("Code ノードが存在しません"));

	private string? _condition = null;
	/// <summary>
	/// 最大震度の状態<br/>
	/// 基本は null<br/>
	/// 基準となる震度以上と考えられるが震度の値を入手していない震度観測点が存在し、その他の観測点の最大震度が基準となる震度未満または入電なしの場合に記載される<br/>
	/// （当面は震度５弱以上となっているため、 震度５弱以上未入電 が入る）
	/// </summary>
	public string? Condition => _condition ??= (Node.TryFindStringNode(Literals.Condition(), out var n) ? n : null);

	private string? _maxInt = null;
	/// <summary>
	/// 最大震度<br/>
	/// 基準となる震度以上（当面は震度５弱以上とする）と考えられるが震度の値を入手していない震度観測点のみしか存在しない場合 null<br/>
	/// 基準となる震度未満の観測点が最大震度となっているが、基準となる震度以上と考えられるが震度の値を入手していない震度観測点が存在する場合、 <seealso cref="Condition"/> に値がセットされる
	/// </summary>
	public string? MaxInt => _maxInt ??= (Node.TryFindStringNode(Literals.MaxInt(), out var n) ? n : null);

	private string? _maxLgInt = null;
	/// <summary>
	/// 最大長周期地震動階級
	/// </summary>
	public string? MaxLgInt => _maxLgInt ??= (Node.TryFindStringNode(Literals.MaxLgInt(), out var n) ? n : null);

	private string? _revise = null;
	/// <summary>
	/// 情報の更新（地域）<br/>
	/// 当該市町村が新規に追加または更新される場合以外は null<br/>
	/// 例: 追加、上方修正
	/// </summary>
	public string? Revise => _revise ??= (Node.TryFindStringNode(Literals.Revise(), out var n) ? n : null);

	/// <summary>
	/// 観測点毎の震度の観測状況
	/// </summary>
	public IEnumerable<IntensityStation> IntensityStations
		=> Node.Children.Where(c => c.Name == Literals.IntensityStation()).Select(c => new IntensityStation(c));
}
