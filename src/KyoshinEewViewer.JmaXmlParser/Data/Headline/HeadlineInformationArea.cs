namespace KyoshinEewViewer.JmaXmlParser.Data.Headline;

[JmaXmlType]
public sealed partial class HeadlineInformationArea<TView>
{
	/// <summary>
	/// 対象地域名
	/// </summary>
	[JmaXml("Name!")]
	public partial string Name { get; }

	/// <summary>
	/// 対象地域コード
	/// </summary>
	[JmaXml("Code!")]
	public partial string Code { get; }
}
