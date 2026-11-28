namespace KyoshinEewViewer.JmaXmlParser.Data.Tsunami;

[JmaXmlType]
public sealed partial class TsunamiData<TView>
{
	/// <summary>
	/// 津波の予測に関する情報
	/// </summary>
	[JmaXml("Forecast?")]
	public partial TsunamiForecast<TView>? Forecast { get; }

	/// <summary>
	/// 津波の観測に関する情報
	/// </summary>
	[JmaXml("Observation?")]
	public partial TsunamiObservation<TView>? Observation { get; }
}
