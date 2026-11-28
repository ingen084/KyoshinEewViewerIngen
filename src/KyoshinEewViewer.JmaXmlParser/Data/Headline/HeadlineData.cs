namespace KyoshinEewViewer.JmaXmlParser.Data.Headline;

[JmaXmlType]
public sealed partial class HeadlineData<TView>
{
	/// <summary>
	/// 電文の内容を簡潔に伝える文章 空の場合もある
	/// </summary>
	[JmaXml("Text!")]
	public partial string Text { get; }

	/// <summary>
	/// 事項種別と対象地域
	/// <para>(地震火山) <seealso href="https://dmdata.jp/docs/jma/manual/0101-0183.pdf#page=10"/></para>
	/// </summary>
	[JmaXml("Information*")]
	public partial JmaXmlList<HeadlineInformation<TView>> Informations { get; }
}
