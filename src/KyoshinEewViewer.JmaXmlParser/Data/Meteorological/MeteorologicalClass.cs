using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;

/// <summary>
/// 危険度や基準到達クラスなど、名称とコードで表す分類。
/// </summary>
public struct MeteorologicalClass(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	/// <summary>
	/// 分類の種類。存在しない場合は null。
	/// </summary>
	public string? Type => Node.TryFindStringAttribute("type"u8, out var n) ? n : null;

	/// <summary>
	/// 時刻の参照番号。存在しない場合は null。
	/// </summary>
	public int? RefId => Node.TryFindIntAttribute(Literals.AttrRefId(), out var n) ? n : null;

	/// <summary>
	/// 分類名。
	/// </summary>
	public string Name => Node.TryFindStringNode("Name"u8, out var n) ? n : throw new JmaXmlParseException("Name ノードが存在しません");

	/// <summary>
	/// 分類コード。先頭のゼロを保持する。
	/// </summary>
	public string Code => Node.TryFindStringNode("Code"u8, out var n) ? n : throw new JmaXmlParseException("Code ノードが存在しません");

	/// <summary>
	/// 分類の文章表現。存在しない場合は null。
	/// </summary>
	public string? Sentence => Node.TryFindStringNode("Sentence"u8, out var n) ? n : null;

	/// <summary>
	/// 文章の種類。存在しない場合は null。
	/// </summary>
	public string? SentenceType => Node.TryFindChild("Sentence"u8, out var n) && n.TryFindStringAttribute(Literals.AttrType(), out var value) ? value : null;

	/// <summary>
	/// 状況。存在しない場合は null。
	/// </summary>
	public string? Condition => Node.TryFindStringNode("Condition"u8, out var n) ? n : null;

	/// <summary>
	/// 補足情報。存在しない場合は null。
	/// </summary>
	public string? Remark => Node.TryFindStringNode("Remark"u8, out var n) ? n : null;
}
