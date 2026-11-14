using System;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;

/// <summary>
/// 警報・注意報の前回の内容または今後の切替予定。
/// </summary>
public struct MeteorologicalNextKind(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	/// <summary>
	/// 種別名。
	/// </summary>
	public string Name => Node.TryFindStringNode("Name"u8, out var n) ? n : throw new JmaXmlParseException("Name ノードが存在しません");

	/// <summary>
	/// 種別コード。先頭のゼロを保持する。
	/// </summary>
	public string Code => Node.TryFindStringNode("Code"u8, out var n) ? n : throw new JmaXmlParseException("Code ノードが存在しません");

	/// <summary>
	/// 状況。存在しない場合は null。
	/// </summary>
	public string? Condition => Node.TryFindStringNode("Condition"u8, out var n) ? n : null;

	/// <summary>
	/// 切替予定時刻。存在しない場合は null。
	/// </summary>
	public DateTimeOffset? DateTime => Node.TryFindDateTimeNode("DateTime"u8, out var n) ? n : null;

	/// <summary>
	/// 切替予定時刻の種類。存在しない場合は null。
	/// </summary>
	public string? DateTimeType => Node.TryFindChild(Literals.DateTime(), out var n) && n.TryFindStringAttribute(Literals.AttrType(), out var value) ? value : null;

	/// <summary>
	/// 切替予定を表す文章。存在しない場合は null。
	/// </summary>
	public string? Sentence => Node.TryFindStringNode("Sentence"u8, out var n) ? n : null;
}
