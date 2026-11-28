namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;

[JmaXmlType]
public sealed partial class WarningAreaPart<TView>
{
	/// <summary>
	/// 風域の種類<br/>
	/// <list type="bullet">
	/// <item>強風域</item>
	/// <item>暴風域</item>
	/// <item>暴風警戒域</item>
	/// </list>
	/// </summary>
	[JmaXml("@type!")]
	public partial string Type { get; }

	/// <summary>
	/// 暴風域･強風域の風速
	/// </summary>
	[JmaXml("jmx_eb:WindSpeed*")]
	public partial JmaXmlList<PhysicalQuantity<TView>> WindSpeeds { get; }

	/// <summary>
	/// 暴風域･強風域の風域情報
	/// </summary>
	[JmaXml("jmx_eb:Circle!")]
	public partial TyphoonCircle<TView> Circle { get; }
}
