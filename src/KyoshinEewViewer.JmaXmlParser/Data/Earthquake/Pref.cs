using System;
using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

/// <summary>
/// 地震情報における都道府県
/// </summary>
public struct Pref(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	private string? _name = null;
	/// <summary>
	/// 都道府県名
	/// </summary>
	public string Name => _name ??= (Node.TryFindStringNode(Literals.Name(), out var n) ? n : throw new JmaXmlParseException("Name ノードが存在しません"));

	private int? _code = null;
	/// <summary>
	/// 都道府県コード
	/// </summary>
	public int Code => _code ??= (Node.TryFindIntNode(Literals.Code(), out var n) ? n : throw new JmaXmlParseException("Code ノードが存在しません"));

	private Category? _category = null;
	/// <summary>
	/// 警報等の種類。存在しない場合は null。
	/// </summary>
	public Category? Category => _category ??= (Node.TryFindChild(Literals.Category(), out var n) ? new(n) : null);

	private IntensityRange? _forecastInt = null;
	/// <summary>
	/// 予測震度の範囲。存在しない場合は null。
	/// </summary>
	public IntensityRange? ForecastInt => _forecastInt ??= (Node.TryFindChild(Literals.ForecastInt(), out var n) ? new(n) : null);

	private IntensityRange? _forecastLgInt = null;
	/// <summary>
	/// 予測長周期地震動階級の範囲。存在しない場合は null。
	/// </summary>
	public IntensityRange? ForecastLgInt => _forecastLgInt ??= (Node.TryFindChild(Literals.ForecastLgInt(), out var n) ? new(n) : null);

	private DateTimeOffset? _arrivalTime = null;
	/// <summary>
	/// 主要動の到達予測時刻。存在しない場合は null。
	/// </summary>
	public DateTimeOffset? ArrivalTime => _arrivalTime ??= (Node.TryFindDateTimeNode(Literals.ArrivalTime(), out var n) ? n : null);

	private string? _condition = null;
	/// <summary>
	/// 到達状況や最大震度の状態。存在しない場合は null。
	/// </summary>
	public string? Condition => _condition ??= (Node.TryFindStringNode(Literals.Condition(), out var n) ? n : null);

	private string? _maxInt = null;
	/// <summary>
	/// 最大震度<br/>
	/// 基準となる震度以上（当面は震度５弱以上とする）と考えられるが震度の値を入手していない市町村のみしか存在しない場合 null
	/// </summary>
	public string? MaxInt => _maxInt ??= (Node.TryFindStringNode(Literals.MaxInt(), out var n) ? n : null);

	private string? _maxLgInt = null;
	/// <summary>
	/// 最大長周期地震動階級
	/// </summary>
	public string? MaxLgInt => _maxLgInt ??= (Node.TryFindStringNode(Literals.MaxLgInt(), out var n) ? n : null);

	private string? _revise = null;
	/// <summary>
	/// 情報の更新（都道府県）<br/>
	/// 当該都道府県が新規に追加されるか更新される場合以外は null<br/>
	/// 例: 追加、上方修正
	/// </summary>
	public string? Revise => _revise ??= (Node.TryFindStringNode(Literals.Revise(), out var n) ? n : null);

	/// <summary>
	/// 地域毎の震度の観測状況
	/// </summary>
	public IEnumerable<Area> Areas
		=> Node.Children.Where(c => c.Name == Literals.Area()).Select(c => new Area(c));
}
