using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Tsunami;

public struct TsunamiData(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	private TsunamiForecast? _forecast = null;
	/// <summary>
	/// 津波の予測に関する情報
	/// </summary>
	public TsunamiForecast? Forecast => _forecast ??= (Node.TryFindChild(Literals.Forecast(), out var n) ? new(n) : null);

	private TsunamiObservation? _observation = null;
	/// <summary>
	/// 津波の観測に関する情報
	/// </summary>
	public TsunamiObservation? Observation => _observation ??= (Node.TryFindChild(Literals.Observation(), out var n) ? new(n) : null);
}
