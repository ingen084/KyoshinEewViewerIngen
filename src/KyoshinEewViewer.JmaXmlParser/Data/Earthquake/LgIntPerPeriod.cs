namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

[JmaXmlType]
public sealed partial class LgIntPerPeriod<TView>
{
	/// <summary>
	/// 周期帯
	/// </summary>
	[JmaXml("@PeriodicBand!")]
	public partial int PeriodicBand { get; }

	/// <summary>
	/// 周期帯(単位)
	/// </summary>
	[JmaXml("@PeriodUnit!")]
	public partial string PeriodUnit { get; }

	/// <summary>
	/// 長周期地震動階級
	/// </summary>
	[JmaXml(".")]
	public partial string Value { get; }
}
