using System;

namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;

[JmaXmlType]
public sealed partial class MeteorologicalInfo<TView>
{
	/// <summary>
	/// 予報や観測時刻
	/// </summary>
	[JmaXml("DateTime!")]
	public partial DateTimeOffset DateTime { get; }

	/// <summary>
	/// 予報や観測時刻の種類
	/// </summary>
	[JmaXml("DateTime!@type!")]
	public partial string? DateTimeType { get; }

	/// <summary>
	/// 各気象情報の予報や観測事項
	/// </summary>
	[JmaXml("Item?/Kind*/Property!")]
	public partial JmaXmlList<MeteorologicalInfoKindProperty<TView>> MeteorologicalInfoKindProperties { get; }

	/// <summary>
	/// 熱帯低気圧の強風域･暴風域の情報
	/// </summary>
	[JmaXml("Item!/Area!/jmx_eb:Circle*")]
	public partial JmaXmlList<TyphoonCircle<TView>> TyphoonCircles { get; }
}
