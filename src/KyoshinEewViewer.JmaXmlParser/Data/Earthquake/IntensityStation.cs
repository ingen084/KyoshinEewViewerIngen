using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

public struct IntensityStation
{
	private XmlNode Node { get; set; }

	public IntensityStation(XmlNode node)
	{
		Node = node;
	}

	private string? _name = null;
	/// <summary>
	/// 観測点名
	/// </summary>
	public string Name => _name ??= (Node.TryFindStringNode(Literals.Name(), out var n) ? n : throw new JmaXmlParseException("Name ノードが存在しません"));

	private string? _code = null;
	/// <summary>
	/// 観測点コード
	/// </summary>
	public string Code => _code ??= (Node.TryFindStringNode(Literals.Code(), out var n) ? n : throw new JmaXmlParseException("Code ノードが存在しません"));

	private string? _int = null;
	/// <summary>
	/// 最大震度<br/>
	/// 基準となる震度以上（当面は震度５弱以上とする）と考えられるが震度の値を入手していない市町村のみしか存在しない場合 震度５弱以上未入電 になる
	/// </summary>
	public string Int => _int ??= (Node.TryFindStringNode(Literals.Int(), out var n) ? n : throw new JmaXmlParseException("Int ノードが存在しません"));

	private string? _revise = null;
	/// <summary>
	/// 情報の更新（地域）<br/>
	/// 当該観測点が新規に追加または更新される場合以外は null<br/>
	/// 例: 追加、上方修正、下方修正
	/// </summary>
	public string? Revise => _revise ??= (Node.TryFindStringNode(Literals.Revise(), out var n) ? n : null);

	private Sva? _sva = null;
	/// <summary>
	/// 絶対速度応答スペクトルの 1.6 秒から 7.8 秒までの周期帯における最大値
	/// </summary>
	public Sva? Sva => _sva ??= (Node.TryFindChild(Literals.Sva(), out var n) ? new(n) : null);

	/// <summary>
	/// 各周期毎の長周期地震動階級の観測状況
	/// </summary>
	public IEnumerable<LgIntPerPeriod> LgIntPerPeriods
		=> Node.Children.Where(c => c.Name == Literals.LgIntPerPeriod()).Select(c => new LgIntPerPeriod(c));

	/// <summary>
	/// 各周期毎の加速度
	/// </summary>
	public IEnumerable<SvaPerPeriod> SvaPerPeriods
		=> Node.Children.Where(c => c.Name == Literals.SvaPerPeriod()).Select(c => new SvaPerPeriod(c));
}
