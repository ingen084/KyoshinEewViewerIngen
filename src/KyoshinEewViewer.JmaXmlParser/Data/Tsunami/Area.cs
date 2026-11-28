namespace KyoshinEewViewer.JmaXmlParser.Data.Tsunami;

[JmaXmlType]
public sealed partial class Area<TView>
{
	/// <summary>
	/// 地域名
	/// </summary>
	[JmaXml("Name!")]
	public partial string Name { get; }

	/// <summary>
	/// 地域コード
	/// </summary>
	[JmaXml("Code!")]
	public partial int Code { get; }
}
