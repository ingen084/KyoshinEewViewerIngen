using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Tsunami;

public struct TsunamiData(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	private string? _release = null;
	/// <summary>
	/// 津波警報等の解除に関する情報。存在しない場合は null。
	/// </summary>
	public string? Release => _release ??= (Node.TryFindStringNode("Release"u8, out var n) ? n : null);

	private TsunamiEstimation? _estimation = null;
	/// <summary>
	/// 沖合の観測に基づく沿岸地域の津波推定値。存在しない場合は null。
	/// </summary>
	public TsunamiEstimation? Estimation => _estimation ??= (Node.TryFindChild("Estimation"u8, out var n) ? new(n) : null);

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
