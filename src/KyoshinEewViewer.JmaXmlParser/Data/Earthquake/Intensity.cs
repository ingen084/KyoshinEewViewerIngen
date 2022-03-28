using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

public struct Intensity
{
	private XmlNode Node { get; set; }

	public Intensity(XmlNode node)
	{
		Node = node;
	}

	private IntensityForecast? forecast = null;
	/// <summary>
	/// 震度の予測に関する情報
	/// <para>「リアルタイム震度電文」の場合 null になる</para>
	/// </summary>
	public IntensityForecast? Forecast => forecast ??= (Node.TryFindChild(Literals.Forecast(), out var n) ? new(n) : null);

	private IntensityObservation? obserbation = null;
	/// <summary>
	/// 震度の観測に関する情報
	/// <para>観測情報が存在しない電文の場合 null になる</para>
	/// </summary>
	public IntensityObservation? Observation => obserbation ??= (Node.TryFindChild(Literals.Observation(), out var n) ? new(n) : null);
}
