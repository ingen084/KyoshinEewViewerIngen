namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

/// <summary>
/// 震源情報
/// </summary>
[JmaXmlType]
public sealed partial class Hypocenter<TView>
{
	/// <summary>
	/// 震源位置
	/// </summary>
	[JmaXml("Area!")]
	public partial HypocenterArea<TView> Area { get; }

	/// <summary>
	/// 精度情報
	/// </summary>
	[JmaXml("Accuracy!")]
	public partial HypocenterAccuracy<TView> Accuracy { get; }
}
