namespace KyoshinEewViewer.JmaXmlParser.Data.Headline;

[JmaXmlType]
public sealed partial class HeadlineInformation<TView>
{
	/// <summary>
	/// 事項種別
	/// </summary>
	[JmaXml("@type!")]
	public partial string Type { get; }

	/// <summary>
	/// 事項種別と対象地域
	/// </summary>
	[JmaXml("Item*")]
	public partial JmaXmlList<HeadlineInformationItem<TView>> Items { get; }
}
