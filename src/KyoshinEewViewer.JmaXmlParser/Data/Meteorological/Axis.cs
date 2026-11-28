namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;

[JmaXmlType]
public sealed partial class Axis<TView>
{
	/// <summary>
	/// 予報円の半径の方向<br/>
	/// condition および description の値は “全域”
	/// </summary>
	[JmaXml("jmx_eb:Direction!")]
	public partial PhysicalQuantity<TView> Direction { get; }

	/// <summary>
	/// 予報円/暴風域/強風域の半径
	/// </summary>
	[JmaXml("jmx_eb:Radius*")]
	public partial JmaXmlList<PhysicalQuantity<TView>> Radiuses { get; }
}
