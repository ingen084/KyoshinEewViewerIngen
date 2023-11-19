using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

public struct Intensity(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	private IntensityForecast? _forecast = null;
	/// <summary>
	/// 震度の予測に関する情報
	/// <para>「リアルタイム震度電文」の場合 null になる</para>
	/// </summary>
	public IntensityForecast? Forecast => _forecast ??= (Node.TryFindChild(Literals.Forecast(), out var n) ? new(n) : null);

	private IntensityObservation? _obserbation = null;
	/// <summary>
	/// 震度の観測に関する情報
	/// <para>観測情報が存在しない電文の場合 null になる</para>
	/// </summary>
	public IntensityObservation? Observation => _obserbation ??= (Node.TryFindChild(Literals.Observation(), out var n) ? new(n) : null);
}
