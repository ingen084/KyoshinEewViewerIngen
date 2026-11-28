namespace KyoshinEewViewer.JmaXmlParser.Data;

[JmaXmlType]
public sealed partial class Kind<TView>
{
	/// <summary>
	/// 事項種別名
	/// </summary>
	[JmaXml("Name!")]
	public partial string Name { get; }

	/// <summary>
	/// 事項種別コード<br/>
	/// 存在しない場合は <c>null</c>
	/// </summary>
	[JmaXml("Code?")]
	public partial string? Code { get; }

	/// <summary>
	/// 事項の状態<br/>
	/// 例: <c>土砂災害、浸水害</c><br/>
	/// 存在しない場合は <c>null</c>
	/// </summary>
	[JmaXml("Condition?")]
	public partial string? Condition { get; }
}
