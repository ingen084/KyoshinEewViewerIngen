namespace KyoshinEewViewer.JmaXmlParser.Data;

[JmaXmlType]
public sealed partial class Category<TView>
{
	/// <summary>
	/// 事項種別
	/// </summary>
	[JmaXml("Kind!")]
	public partial Kind<TView> Kind { get; }

	/// <summary>
	/// 前回の事項種別<br/>
	/// 存在しない場合は <c>null</c>
	/// </summary>
	[JmaXml("LastKind?")]
	public partial Kind<TView>? LastKind { get; }
}
