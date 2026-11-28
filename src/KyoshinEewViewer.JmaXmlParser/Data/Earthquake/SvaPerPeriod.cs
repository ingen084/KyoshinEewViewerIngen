namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

[JmaXmlType]
public sealed partial class SvaPerPeriod<TView>
{
	/// <summary>
	/// 単位
	/// </summary>
	[JmaXml("@unit!")]
	public partial string Unit { get; }

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
	/// 加速度
	/// </summary>
	[JmaXml(".")]
	public partial float Value { get; }
}
