using System;

namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

/// <summary>
/// 地震情報<br/>
/// Body内 <c>Earthquake</c> タグに対応する
/// </summary>
[JmaXmlType]
public sealed partial class EarthquakeData<TView>
{
	/// <summary>
	/// 地震の発生した時刻（発震時刻）。秒値まで有効<br/>
	/// <list type="bullet">
	///		<item>「緊急地震速報（地震動予報）」及び「緊急地震速報（予報）」において、非常に強い揺れを検知・最大予測震度のみの場合、null</item>
	///		<item>「仮定震源要素」を設定する場合、PLUM 法でトリガー条件を最初に満足した観測点における発現時刻を元に算出した地震発生時刻</item>
	/// </list>
	/// </summary>
	[JmaXml("OriginTime?")]
	public partial DateTimeOffset? OriginTime { get; }

	/// <summary>
	/// 震源要素の補足情報<br/>
	/// 記載されている震源要素が仮定震源要素である場合、 "<c>仮定震源要素</c>" が入った状態になる
	/// </summary>
	[JmaXml("Condition?")]
	public partial string? Condition { get; }

	/// <summary>
	/// 観測点で地震を検知した時刻（発現時刻）。秒値まで有効
	/// </summary>
	[JmaXml("ArrivalTime!")]
	public partial DateTimeOffset? ArrivalTime { get; }

	/// <summary>
	/// 地震の位置に関する要素（震央地名、震源要素等）
	/// <para>留意事項
	/// <list type="bullet">
	///		<item>「緊急地震速報（地震動予報）」及び「緊急地震速報（予報）」において、非常に強い揺れを検知・最大予測震度のみの場合、非常に強い揺れを検知した観測点の座標になる<br/>
	///		（震源の深さは 10km として扱う）</item>
	///		<item>「仮定震源要素」を設定する場合、PLUM 法でトリガー条件を最初に満足した観測点の座標になる<br/>
	///		（震源の深さは 10kmとする）</item>
	/// </list>
	/// </para>
	/// </summary>
	[JmaXml("Hypocenter!")]
	public partial Hypocenter<TView> Hypocenter { get; }

	/// <summary>
	/// 地震のマグニチュードの値
	/// <para><seealso cref="PhysicalQuantity.Type"/> にマグニチュードの種別(Mj,Mw等)<br/>
	/// <seealso cref="PhysicalQuantity.Description"/> に文字列表現</para>
	/// <para>マグニチュードが不明の場合、 <seealso cref="PhysicalQuantity.Condition"/> が "不明" となり値は "NaN" になる</para>
	/// <para>「仮定震源要素」の場合、値は "1.0" 固定</para>
	/// </summary>
	[JmaXml("jmx_eb:Magnitude!")]
	public partial PhysicalQuantity<TView> Magnitude { get; }
}
