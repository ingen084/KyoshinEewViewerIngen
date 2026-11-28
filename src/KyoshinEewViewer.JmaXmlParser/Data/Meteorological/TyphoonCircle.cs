namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;

[JmaXmlType]
public sealed partial class TyphoonCircle<TView>
{
	/// <summary>
	/// 風域の種類<br/>
	/// <list type="bullet">
	/// <item>強風域</item>
	/// <item>予報円</item>
	/// </list>
	/// </summary>
	[JmaXml("@type!")]
	public partial string Type { get; }

	/// <summary>
	/// 強風域及び予報円の中心位置
	/// </summary>
	[JmaXml("jmx_eb:BasePoint*")]
	public partial JmaXmlList<Coordinate<TView>> BasePoints { get; }

	/// <summary>
	/// 強風域･暴風域の距離情報
	/// </summary>
	[JmaXml("jmx_eb:Axes!/jmx_eb:Axis*")]
	public partial JmaXmlList<Axis<TView>> Axes { get; }
}
