namespace KyoshinEewViewer.JmaXmlParser.Data;

[JmaXmlType]
public sealed partial class CodeDefineType<TView>
{
	/// <summary>
	/// 定義したコードを使用する要素の相対的な出現位置
	/// </summary>
	[JmaXml("@xpath!")]
	public partial string XPath { get; }

	/// <summary>
	/// コード体系
	/// </summary>
	[JmaXml(".")]
	public partial string Value { get; }
}
