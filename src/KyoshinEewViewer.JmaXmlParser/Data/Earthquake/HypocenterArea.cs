namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

[JmaXmlType]
public sealed partial class HypocenterArea<TView>
{
	/// <summary>
	/// 震央地名
	/// </summary>
	[JmaXml("Name!")]
	public partial string Name { get; }

	/// <summary>
	/// 震央地名コード
	/// </summary>
	[JmaXml("Code!")]
	public partial string Code { get; }

	/// <summary>
	/// 震源要素(複数ある場合は1件目のみ)
	/// </summary>
	[JmaXml("jmx_eb:Coordinate!")]
	public partial Coordinate<TView> Coordinate { get; }

	/// <summary>
	/// 震源要素<br/>
	/// 顕著な地震の震源要素更新のお知らせ の場合は1件以上となる
	/// </summary>
	[JmaXml("jmx_eb:Coordinate*")]
	public partial JmaXmlList<Coordinate<TView>> Coordinates { get; }

	/// <summary>
	/// 短縮用震央地名<br/>
	/// EEW以外は null
	/// </summary>
	[JmaXml("ReduceName?")]
	public partial string? ReduceName { get; }

	/// <summary>
	/// 短縮用震央地名コード<br/>
	/// EEW以外は null
	/// </summary>
	[JmaXml("ReduceCode?")]
	public partial string? ReduceCode { get; }

	/// <summary>
	/// 内陸判定<br/>
	/// 例: 内陸、海域<br/>
	/// 以下の場合は null
	/// <list type="bullet">
	///		<item>EEW以外</item>
	///		<item>「緊急地震速報（地震動予報）」、および「緊急地震速報（予報）」において、非常に強い揺れを検知・最大予測震度のみの場合</item>
	///		<item>「仮定震源要素」の場合</item>
	/// </list>
	/// は null
	/// </summary>
	[JmaXml("LandOrSea?")]
	public partial string? LandOrSea { get; }

	/// <summary>
	/// 詳細震央地名<br/>
	/// 国外で発生した地震について、震源地の詳細な位置
	/// </summary>
	[JmaXml("DetailedName?")]
	public partial string? DetailedName { get; }

	/// <summary>
	/// 詳細震央地名コード<br/>
	/// コード種別 は 詳細震央地名
	/// </summary>
	[JmaXml("DetailedCode?")]
	public partial string? DetailedCode { get; }

	/// <summary>
	/// 震央補助表現<br/>
	/// 日本近海で発生し、津波警報・注意報を発表した地震について、震源地の詳細な位置を示すための目印となる地名
	/// </summary>
	[JmaXml("NameFromMark?")]
	public partial string? NameFromMark { get; }

	/// <summary>
	/// 震源決定機関<br/>
	/// 国外で発生した地震について、気象庁以外の機関で決定された震源要素を採用して情報発表した場合<br/>
	/// 例: PTWC、WCATWC、USGS
	/// </summary>
	[JmaXml("Source?")]
	public partial string? Source { get; }
}
