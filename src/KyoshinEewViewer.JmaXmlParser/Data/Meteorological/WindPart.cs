namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;

[JmaXmlType]
public sealed partial class WindPart<TView>
{
	/// <summary>
	/// 予報円の中心位置<br/>
	/// type が 最大風速 の場合、 condition は、値が "中心付近"、"中心付近を除く"、"なし" の場合がある
	/// </summary>
	[JmaXml("jmx_eb:WindSpeed*")]
	public partial JmaXmlList<PhysicalQuantity<TView>> WindSpeeds { get; }
}
