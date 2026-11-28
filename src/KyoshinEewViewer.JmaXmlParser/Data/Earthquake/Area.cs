using System;

namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

[JmaXmlType]
public sealed partial class Area<TView>
{
	/// <summary>
	/// 地域名
	/// </summary>
	[JmaXml("Name!")]
	public partial string Name { get; }

	/// <summary>
	/// 地域コード
	/// </summary>
	[JmaXml("Code!")]
	public partial int Code { get; }

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
	/// 主要動の到達予測時刻<br/>
	/// 緊急地震速報で、主要動が未到達と推測される場合に記載される。<see cref="Condition"/> とは同時に出現しない
	/// <para>PLUM 法で予測している区域では、その震度（階級震度）を初めて予測した時刻になる</para>
	/// </summary>
	[JmaXml("ArrivalTime?")]
	public partial DateTimeOffset? ArrivalTime { get; }

	/// <summary>
	/// 主要動の到達に関する状況 <c>既に主要動到達と推測</c><br/>
	/// 緊急地震速報で、主要動の到達予測時刻を過ぎている場合に記載される。<see cref="ArrivalTime"/> とは同時に出現しない
	/// </summary>
	[JmaXml("Condition?")]
	public partial string? Condition { get; }

	/// <summary>
	/// 最大震度<br/>
	/// 基準となる震度以上（当面は震度５弱以上とする）と考えられるが震度の値を入手していない市町村のみしか存在しない場合 null
	/// </summary>
	[JmaXml("MaxInt?")]
	public partial string? MaxInt { get; }

	/// <summary>
	/// 最大長周期地震動階級
	/// </summary>
	[JmaXml("MaxLgInt?")]
	public partial string? MaxLgInt { get; }

	/// <summary>
	/// 情報の更新（地域）<br/>
	/// 当該地域が新規に追加または更新される場合以外は null<br/>
	/// 例: 追加、上方修正
	/// </summary>
	[JmaXml("Revise?")]
	public partial string? Revise { get; }

	/// <summary>
	/// 警報等の種類
	/// </summary>
	[JmaXml("Category!")]
	public partial Category<TView>? Category { get; }

	/// <summary>
	/// 地域毎の震度の観測状況
	/// </summary>
	[JmaXml("City*")]
	public partial JmaXmlList<City<TView>> Cities { get; }

	/// <summary>
	/// 観測点毎の震度の観測状況
	/// </summary>
	[JmaXml("IntensityStation*")]
	public partial JmaXmlList<IntensityStation<TView>> IntensityStations { get; }
}
