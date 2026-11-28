namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

[JmaXmlType]
public sealed partial class IntensityForecast<TView>
{
	/// <summary>
	/// コード体系の定義
	/// <para>「緊急地震速報（地震動予報）」及び「緊急地震速報（予報）」のとき、<br/>
	/// 震度予測を行う府県予報区（Pref）、細分区域（Area）が１つも無い場合は空になる</para>
	/// </summary>
	[JmaXml("CodeDefine?/Type*")]
	public partial JmaXmlList<CodeDefineType<TView>> CodeDefineTypes { get; }

	/// <summary>
	/// 最大予測震度の下限 <c>0~7,不明</c>
	/// <para>通常上限と同じだが、「～程度以上」と表現するときのみ上限は 「over」となる</para>
	/// </summary>
	[JmaXml("ForecastInt!/From!")]
	public partial string ForecastIntFrom { get; }

	/// <summary>
	/// 最大予測震度の上限 <c>0~7,over,不明</c>
	/// <para>通常下限と同じだが、「～程度以上」と表現するときのみ上限は 「over」となる</para>
	/// </summary>
	[JmaXml("ForecastInt!/To!")]
	public partial string ForecastIntTo { get; }

	/// <summary>
	/// 最大予測長周期地震動階級の下限 <c>0~4,不明</c><br/>
	/// 「緊急地震速報（警報）」、「緊急地震速報（地震動予報）」の場合かつ震源が150km未満の場合以外は null
	/// <para>通常上限と同じだが、「～程度以上」と表現するときのみ上限は 「over」となる</para>
	/// </summary>
	[JmaXml("ForecastLgInt?/From!")]
	public partial string? ForecastLgIntFrom { get; }

	/// <summary>
	/// 最大予測長周期地震動階級の上限 <c>0~4,over,不明</c><br/>
	/// 「緊急地震速報（警報）」、「緊急地震速報（地震動予報）」の場合かつ震源が150km未満の場合以外は null
	/// <para>通常下限と同じだが、「～程度以上」と表現するときのみ上限は 「over」となる</para>
	/// </summary>
	[JmaXml("ForecastLgInt?/To!")]
	public partial string? ForecastLgIntTo { get; }

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
	[JmaXml("Appendix?/MaxIntChange!")]
	public partial int? MaxIntChange { get; }

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
	[JmaXml("Appendix?/MaxLgIntChange?")]
	public partial int? MaxLgIntChange { get; }

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
	[JmaXml("Appendix?/MaxIntChangeReason!")]
	public partial int? MaxIntChangeReason { get; }

	/// <summary>
	/// 府県予報区の諸要素
	/// </summary>
	[JmaXml("Pref*")]
	public partial JmaXmlList<Pref<TView>> Prefs { get; }
}
