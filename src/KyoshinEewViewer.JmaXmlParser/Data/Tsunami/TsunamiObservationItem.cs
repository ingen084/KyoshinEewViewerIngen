namespace KyoshinEewViewer.JmaXmlParser.Data.Tsunami;

[JmaXmlType]
public sealed partial class TsunamiObservationItem<TView>
{
	/// <summary>
	/// 津波予報区
	/// </summary>
	[JmaXml("Area!")]
	public partial Area<TView> Area { get; }

	/// <summary>
	/// 潮位観測点
	/// </summary>
	[JmaXml("Station*")]
	public partial JmaXmlList<TsunamiStation<TView>> Stations { get; }
}
