namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;

[JmaXmlType]
public sealed partial class MeteorologicalInfoKindProperty<TView>
{
	/// <summary>
	/// 要素名
	/// </summary>
	[JmaXml("Type!")]
	public partial string Type { get; }

	/// <summary>
	/// Type=呼称 のときに出現
	/// </summary>
	[JmaXml("TyphoonNamePart!")]
	public partial TyphoonNamePart<TView> TyphoonNamePart { get; }

	/// <summary>
	/// Type=階級 のときに出現
	/// </summary>
	[JmaXml("ClassPart!")]
	public partial ClassPart<TView> ClassPart { get; }

	/// <summary>
	/// Type=中心 のときに出現
	/// </summary>
	[JmaXml("CenterPart!")]
	public partial CenterPart<TView> CenterPart { get; }

	/// <summary>
	/// Type=風 のときに出現
	/// </summary>
	[JmaXml("WindPart!")]
	public partial WindPart<TView> WindPart { get; }

	/// <summary>
	/// Type=風 のときに出現
	/// </summary>
	[JmaXml("WarningAreaPart*")]
	public partial JmaXmlList<WarningAreaPart<TView>> WarningAreaParts { get; }
}
