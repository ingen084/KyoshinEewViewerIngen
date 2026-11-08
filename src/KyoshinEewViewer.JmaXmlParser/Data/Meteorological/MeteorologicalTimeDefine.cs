using System;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;

/// <summary>
/// 時系列の refID と対応付ける時刻定義。
/// </summary>
public struct MeteorologicalTimeDefine(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	/// <summary>
	/// 時刻の参照番号。
	/// </summary>
	public int TimeId => Node.TryFindIntAttribute("timeId"u8, out var n) ? n : throw new JmaXmlParseException("timeId 属性が存在しません");

	/// <summary>
	/// 基点時刻。
	/// </summary>
	public DateTimeOffset DateTime => Node.TryFindDateTimeNode(Literals.DateTime(), out var n) ? n : throw new JmaXmlParseException("DateTime ノードが存在しません");

	/// <summary>
	/// 対象期間（ISO 8601 の期間表現）。存在しない場合は null。
	/// </summary>
	public string? Duration => Node.TryFindStringNode("Duration"u8, out var n) ? n : null;

	/// <summary>
	/// 対象期間の文字列表現。存在しない場合は null。
	/// </summary>
	public string? Name => Node.TryFindStringNode("Name"u8, out var n) ? n : null;
}
