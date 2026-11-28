namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

[JmaXmlType]
public sealed partial class Intensity<TView>
{
	/// <summary>
	/// 震度の予測に関する情報
	/// <para>「リアルタイム震度電文」の場合 null になる</para>
	/// </summary>
	[JmaXml("Forecast?")]
	public partial IntensityForecast<TView>? Forecast { get; }

	/// <summary>
	/// 震度の観測に関する情報
	/// <para>観測情報が存在しない電文の場合 null になる</para>
	/// </summary>
	[JmaXml("Observation?")]
	public partial IntensityObservation<TView>? Observation { get; }
}
