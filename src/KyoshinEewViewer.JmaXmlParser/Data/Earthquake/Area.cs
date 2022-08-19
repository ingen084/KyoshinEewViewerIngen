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

	private string? forecastIntFrom = null;
	/// <summary>
	/// 最大予測震度の下限 <c>0~7,不明</c>
	/// <para>通常上限と同じだが、「～程度以上」と表現するときのみ上限は 「over」となる</para>
	/// </summary>
	public string ForecastIntFrom
	{
		get {
			if (forecastIntFrom != null)
				return forecastIntFrom;
			if (!Node.TryFindChild(Literals.ForecastInt(), out var n))
				throw new JmaXmlParseException("ForecastInt ノードが存在しません");
			if (!n.TryFindStringNode(Literals.From(), out var r))
				throw new JmaXmlParseException("From ノードが存在しません");
			return forecastIntFrom = r;
		}
	}

	private string? forecastIntTo = null;
	/// <summary>
	/// 最大予測震度の上限 <c>0~7,over,不明</c>
	/// <para>通常下限と同じだが、「～程度以上」と表現するときのみ上限は 「over」となる</para>
	/// </summary>
	public string ForecastIntTo
	{
		get {
			if (forecastIntTo != null)
				return forecastIntTo;
			if (!Node.TryFindChild(Literals.ForecastInt(), out var n))
				throw new JmaXmlParseException("ForecastInt ノードが存在しません");
			if (!n.TryFindStringNode(Literals.To(), out var r))
				throw new JmaXmlParseException("To ノードが存在しません");
			return forecastIntTo = r;
		}
	}

	private string? forecastLgIntFrom = null;
	/// <summary>
	/// 最大予測長周期地震動階級の下限 <c>0~4,不明</c><br/>
	/// 「緊急地震速報（警報）」、「緊急地震速報（地震動予報）」の場合かつ震源が150km未満の場合以外は null
	/// <para>通常上限と同じだが、「～程度以上」と表現するときのみ上限は 「over」となる</para>
	/// </summary>
	public string? ForecastLgIntFrom
	{
		get {
			if (forecastLgIntFrom != null)
				return forecastLgIntFrom;
			if (!Node.TryFindChild(Literals.ForecastLgInt(), out var n))
				return null;
			if (!n.TryFindStringNode(Literals.From(), out var r))
				throw new JmaXmlParseException("From ノードが存在しません");
			return forecastLgIntFrom = r;
		}
	}

	private string? forecastLgIntTo = null;
	/// <summary>
	/// 最大予測長周期地震動階級の上限 <c>0~4,over,不明</c><br/>
	/// 「緊急地震速報（警報）」、「緊急地震速報（地震動予報）」の場合かつ震源が150km未満の場合以外は null
	/// <para>通常下限と同じだが、「～程度以上」と表現するときのみ上限は 「over」となる</para>
	/// </summary>
	public string? ForecastLgIntTo
	{
		get {
			if (forecastLgIntTo != null)
				return forecastLgIntTo;
			if (!Node.TryFindChild(Literals.ForecastLgInt(), out var n))
				return null;
			if (!n.TryFindStringNode(Literals.To(), out var r))
				throw new JmaXmlParseException("To ノードが存在しません");
			return forecastLgIntTo = r;
		}
	}

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

	private Category? category = null;
	/// <summary>
	/// 警報等の種類
	/// </summary>
	public Category? Category => category ??= (Node.TryFindChild(Literals.Category(), out var c) ? new(c) : throw new JmaXmlParseException("Category ノードが存在しません"));

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
