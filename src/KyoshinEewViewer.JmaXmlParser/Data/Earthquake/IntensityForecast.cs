using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

public struct IntensityForecast(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	/// <summary>
	/// コード体系の定義
	/// <para>「緊急地震速報（地震動予報）」及び「緊急地震速報（予報）」のとき、<br/>
	/// 震度予測を行う府県予報区（Pref）、細分区域（Area）が１つも無い場合は空になる</para>
	/// </summary>
	public IEnumerable<CodeDefineType> CodeDefineTypes
	{
		get {
			if (!Node.TryFindChild(Literals.CodeDefine(), out var n))
				return Enumerable.Empty<CodeDefineType>();
			return n.Children.Where(c => c.Name == Literals.Type()).Select(c => new CodeDefineType(c));
		}
	}

	private string? _forecastIntFrom = null;
	/// <summary>
	/// 最大予測震度の下限 <c>0~7,不明</c>
	/// <para>通常上限と同じだが、「～程度以上」と表現するときのみ上限は 「over」となる</para>
	/// </summary>
	public string ForecastIntFrom
	{
		get {
			if (_forecastIntFrom != null)
				return _forecastIntFrom;
			if (!Node.TryFindChild(Literals.ForecastInt(), out var n))
				throw new JmaXmlParseException("ForecastInt ノードが存在しません");
			if (!n.TryFindStringNode(Literals.From(), out var r))
				throw new JmaXmlParseException("From ノードが存在しません");
			return _forecastIntFrom = r;
		}
	}

	private string? _forecastIntTo = null;
	/// <summary>
	/// 最大予測震度の上限 <c>0~7,over,不明</c>
	/// <para>通常下限と同じだが、「～程度以上」と表現するときのみ上限は 「over」となる</para>
	/// </summary>
	public string ForecastIntTo
	{
		get {
			if (_forecastIntTo != null)
				return _forecastIntTo;
			if (!Node.TryFindChild(Literals.ForecastInt(), out var n))
				throw new JmaXmlParseException("ForecastInt ノードが存在しません");
			if (!n.TryFindStringNode(Literals.To(), out var r))
				throw new JmaXmlParseException("To ノードが存在しません");
			return _forecastIntTo = r;
		}
	}

	private string? _forecastLgIntFrom = null;
	/// <summary>
	/// 最大予測長周期地震動階級の下限 <c>0~4,不明</c><br/>
	/// 「緊急地震速報（警報）」、「緊急地震速報（地震動予報）」の場合かつ震源が150km未満の場合以外は null
	/// <para>通常上限と同じだが、「～程度以上」と表現するときのみ上限は 「over」となる</para>
	/// </summary>
	public string? ForecastLgIntFrom
	{
		get {
			if (_forecastLgIntFrom != null)
				return _forecastLgIntFrom;
			if (!Node.TryFindChild(Literals.ForecastLgInt(), out var n))
				return null;
			if (!n.TryFindStringNode(Literals.From(), out var r))
				throw new JmaXmlParseException("From ノードが存在しません");
			return _forecastLgIntFrom = r;
		}
	}

	private string? _forecastLgIntTo = null;
	/// <summary>
	/// 最大予測長周期地震動階級の上限 <c>0~4,over,不明</c><br/>
	/// 「緊急地震速報（警報）」、「緊急地震速報（地震動予報）」の場合かつ震源が150km未満の場合以外は null
	/// <para>通常下限と同じだが、「～程度以上」と表現するときのみ上限は 「over」となる</para>
	/// </summary>
	public string? ForecastLgIntTo
	{
		get {
			if (_forecastLgIntTo != null)
				return _forecastLgIntTo;
			if (!Node.TryFindChild(Literals.ForecastLgInt(), out var n))
				return null;
			if (!n.TryFindStringNode(Literals.To(), out var r))
				throw new JmaXmlParseException("To ノードが存在しません");
			return _forecastLgIntTo = r;
		}
	}

	private int? _maxIntChange = null;
	/// <summary>
	/// 最大予測震度変化 0~2<br/>
	/// 「緊急地震速報（警報）」、「緊急地震速報（地震動予報）」及び「緊急地震速報（予報）」の場合、<br/>
	/// 震度予測及び長周期地震動階級予測をどちらも行っていないために、直前の緊急地震速報（※）と今回の緊急地震速報の間で最大予測震度及び最大予測長周期地震動階級の比較ができない場合、null<br/>
	/// ※ここでは、直前の「緊急地震速報（警報）」、「緊急地震速報（地震動予報）」または「緊急地震速報（予報）」が比較対象となり、警報と予報の区別はしない。
	/// <para>0. ほとんど変化なし<br/>
	/// 1. 最大予測震度が 1.0 以上大きくなった<br/>
	/// 2. 最大予測震度が 1.0 以上小さくなった<br/>
	/// </para>
	/// </summary>
	public int? MaxIntChange
	{
		get {
			if (_maxIntChange != null)
				return _maxIntChange;
			if (!Node.TryFindChild(Literals.Appendix(), out var n))
				return null;
			if (!n.TryFindIntNode(Literals.MaxIntChange(), out var r))
				throw new JmaXmlParseException("MaxIntChange ノードが存在しません");
			return _maxIntChange = r;
		}
	}

	private int? _maxLgIntChange = null;
	/// <summary>
	/// 最大予測長周期地震動階級変化 0~2<br/>
	/// 「緊急地震速報（警報）」、「緊急地震速報（地震動予報）」及び「緊急地震速報（予報）」の場合、<br/>
	/// 震度予測及び長周期地震動階級予測をどちらも行っていないために、直前の緊急地震速報（※）と今回の緊急地震速報の間で最大予測震度及び最大予測長周期地震動階級の比較ができない場合、null
	/// <para>「緊急地震速報（警報） 」または「緊急地震速報（地震動予報）」ではない、もしくは ForecastLgInt が出現しない場合 null</para>
	/// ※ここでは、直前の「緊急地震速報（警報）」、「緊急地震速報（地震動予報）」または「緊急地震速報（予報）」が比較対象となり、警報と予報の区別はしない。
	/// <para>0. ほとんど変化なし<br/>
	/// 1. 最大予測長周期地震動階級が 1 以上大きくなった<br/>
	/// 2. 最大予測長周期地震動階級が 1 以上小さくなった<br/>
	/// </para>
	/// </summary>
	public int? MaxLgIntChange
	{
		get {
			if (_maxLgIntChange != null)
				return _maxLgIntChange;
			if (!Node.TryFindChild(Literals.Appendix(), out var n))
				return null;
			if (!n.TryFindIntNode(Literals.MaxLgIntChange(), out var r))
				return null;
			return _maxLgIntChange = r;
		}
	}

	private int? _maxIntChangeReason = null;
	/// <summary>
	/// 最大予測値変化の理由 0~4,9<br/>
	/// 「緊急地震速報（警報）」、「緊急地震速報（地震動予報）」及び「緊急地震速報（予報）」の場合、<br/>
	/// 震度予測及び長周期地震動階級予測をどちらも行っていないために、直前の緊急地震速報（※）と今回の緊急地震速報の間で最大予測震度及び最大予測長周期地震動階級の比較ができない場合、null
	/// ※ここでは、直前の「緊急地震速報（警報）」、「緊急地震速報（地震動予報）」または「緊急地震速報（予報）」が比較対象となり、警報と予報の区別はしない。
	/// <para>0. 変化なし<br/>
	/// 1. 主としてＭが変化したため(1.0 以上)<br/>
	/// 2. 主として震央位置が変化したため(10.0km 以上)<br/>
	/// 3. Ｍ及び震央位置が変化したため(1 と 2 の複合条件)<br/>
	/// 4. 震源の深さが変化したため(上記のいずれにもあてはまらず、30.0km 以上の変化)<br/>
	/// 9. PLUM 法による予測により変化したため</para>
	/// </summary>
	public int? MaxIntChangeReason
	{
		get {
			if (_maxIntChangeReason != null)
				return _maxIntChangeReason;
			if (!Node.TryFindChild(Literals.Appendix(), out var n))
				return null;
			if (!n.TryFindIntNode(Literals.MaxIntChangeReason(), out var r))
				throw new JmaXmlParseException("MaxIntChangeReason ノードが存在しません");
			return _maxIntChangeReason = r;
		}
	}

	/// <summary>
	/// 府県予報区の諸要素
	/// </summary>
	public IEnumerable<Pref> Prefs
		=> Node.Children.Where(c => c.Name == Literals.Pref()).Select(c => new Pref(c));
}
