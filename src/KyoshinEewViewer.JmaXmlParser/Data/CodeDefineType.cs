using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data;

public struct CodeDefineType(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	private string? _xpath = null;
	/// <summary>
	/// 定義したコードを使用する要素の相対的な出現位置
	/// </summary>
	public string XPath => _xpath ??= (Node.TryFindStringAttribute(Literals.AttrXPath(), out var n) ? n : throw new JmaXmlParseException("xpath 属性が見つかりません"));

	private string? _value = null;
	/// <summary>
	/// コード体系
	/// </summary>
	public string Value => _value ??= Node.InnerText.ToString();
}
