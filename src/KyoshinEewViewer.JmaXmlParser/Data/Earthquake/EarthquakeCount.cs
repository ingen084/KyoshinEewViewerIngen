namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

[JmaXmlType]
public sealed partial class EarthquakeCount<TView>
{
	/// <summary>
	/// 地震回数（期間別）
	/// </summary>
	[JmaXml("Item*")]
	public partial JmaXmlList<EarthquakeCountItem<TView>> Cities { get; }
}
