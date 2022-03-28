using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

public struct Area
{
	private XmlNode Node { get; set; }

	public Area(XmlNode node)
	{
		Node = node;
	}

	private string? name = null;
	/// <summary>
	/// 地域名
	/// </summary>
	public string Name => name ??= (Node.TryFindStringNode(Literals.Name(), out var n) ? n : throw new JmaXmlParseException("Name ノードが存在しません"));

	private string? code = null;
	/// <summary>
	/// 地域コード
	/// </summary>
	public string Code => code ??= (Node.TryFindStringNode(Literals.Code(), out var n) ? n : throw new JmaXmlParseException("Code ノードが存在しません"));

	private string? maxInt = null;
	/// <summary>
	/// 最大震度<br/>
	/// 基準となる震度以上（当面は震度５弱以上とする）と考えられるが震度の値を入手していない市町村のみしか存在しない場合 null
	/// </summary>
	public string? MaxInt => maxInt ??= (Node.TryFindStringNode(Literals.MaxInt(), out var n) ? n : null);

	private string? maxLgInt = null;
	/// <summary>
	/// 最大長周期地震動階級
	/// </summary>
	public string? MaxLgInt => maxLgInt ??= (Node.TryFindStringNode(Literals.MaxLgInt(), out var n) ? n : null);

	private string? revise = null;
	/// <summary>
	/// 情報の更新（地域）<br/>
	/// 当該地域が新規に追加または更新される場合以外は null<br/>
	/// 例: 追加、上方修正
	/// </summary>
	public string? Revise => revise ??= (Node.TryFindStringNode(Literals.Revise(), out var n) ? n : null);

	/// <summary>
	/// 地域毎の震度の観測状況
	/// </summary>
	public IEnumerable<City> Cities
		=> Node.Children.Where(c => c.Name == Literals.City()).Select(c => new City(c));

	/// <summary>
	/// 観測点毎の震度の観測状況
	/// </summary>
	public IEnumerable<IntensityStation> IntensityStations
		=> Node.Children.Where(c => c.Name == Literals.IntensityStation()).Select(c => new IntensityStation(c));
}
