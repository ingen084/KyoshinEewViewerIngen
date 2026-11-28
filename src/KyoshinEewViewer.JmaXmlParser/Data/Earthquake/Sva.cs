namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

[JmaXmlType]
public sealed partial class Sva<TView>
{
	/// <summary>
	/// 単位
	/// </summary>
	[JmaXml("@unit!")]
	public partial string Unit { get; }

	/// <summary>
	/// 加速度
	/// </summary>
	[JmaXml(".")]
	public partial float Value { get; }
}
