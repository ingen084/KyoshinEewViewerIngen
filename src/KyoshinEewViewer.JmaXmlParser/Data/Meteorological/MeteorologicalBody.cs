namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;

[JmaXmlType]
public sealed partial class MeteorologicalBody<TView>
{
	/// <summary>
	/// 一連の気象情報の種類
	/// </summary>
	[JmaXml("MeteorologicalInfos!@type!")]
	public partial string Type { get; }

	/// <summary>
	/// 各気象情報の予報や観測事項
	/// </summary>
	[JmaXml("MeteorologicalInfos?/MeteorologicalInfo*")]
	public partial JmaXmlList<MeteorologicalInfo<TView>> MeteorologicalInfos { get; }
}
