namespace KyoshinEewViewer.JmaXmlParser.Data.Headline;

[JmaXmlType]
public sealed partial class HeadlineInformationItem<TView>
{
	/// <summary>
	/// 事項種別(先頭1件のみ)
	/// </summary>
	[JmaXml("Kind!")]
	public partial Kind<TView> Kind { get; }

	/// <summary>
	/// 事項種別(全件)
	/// </summary>
	[JmaXml("Kind*")]
	public partial JmaXmlList<Kind<TView>> Kinds { get; }

	/// <summary>
	/// 事項種別(変化前/先頭1件のみ)<br/>
	/// 存在しない場合 <c>null</c>
	/// </summary>
	[JmaXml("LastKind?")]
	public partial Kind<TView>? LastKind { get; }

	/// <summary>
	/// 事項種別(変化前/全件)
	/// </summary>
	[JmaXml("LastKind*")]
	public partial JmaXmlList<Kind<TView>> LastKinds { get; }

	/// <summary>
	/// エリアコードの種類 存在しない場合もある
	/// </summary>
	[JmaXml("Areas?@codeType!")]
	public partial string? AreaCodeType { get; }

	/// <summary>
	/// 対象地域 存在しない場合もある
	/// </summary>
	[JmaXml("Areas?/Area*")]
	public partial JmaXmlList<HeadlineInformationArea<TView>> Areas { get; }
}
