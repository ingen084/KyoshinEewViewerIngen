using System;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

public struct EarthquakeCountItem
{
	private XmlNode Node { get; set; }

	public EarthquakeCountItem(XmlNode node)
	{
		Node = node;
	}

	private string? type = null;
	/// <summary>
	/// 期間幅の種類<br/>
	/// "１時間地震回数"（1 時間単位）、"累積地震回数"（全期間の合計）、"地震回数"（その他の場合）
	/// </summary>
	public string Type => type ??= (Node.TryFindStringAttribute(Literals.AttrType(), out var n) ? n : throw new JmaXmlParseException("Type 属性が存在しません"));

	private DateTimeOffset? startTime = null;
	/// <summary>
	/// 開始時刻
	/// </summary>
	public DateTimeOffset StartTime => startTime ??= (Node.TryFindDateTimeNode(Literals.StartTime(), out var n) ? n : throw new JmaXmlParseException("StartTime ノードが存在しません"));

	private DateTimeOffset? endTime = null;
	/// <summary>
	///終了時刻
	/// </summary>
	public DateTimeOffset EndTime => endTime ??= (Node.TryFindDateTimeNode(Literals.EndTime(), out var n) ? n : throw new JmaXmlParseException("EndTime ノードが存在しません"));

	private string? number = null;
	/// <summary>
	/// 無感地震を含む全ての地震回数<br/>
	/// 発表しない場合は -1
	/// </summary>
	public string Number => number ??= (Node.TryFindStringNode(Literals.Number(), out var n) ? n : throw new JmaXmlParseException("Int ノードが存在しません"));

	private string? feltNumber = null;
	/// <summary>
	/// 有感地震回数<br/>
	/// 発表しない場合は -1
	/// </summary>
	public string FeltNumber => feltNumber ??= (Node.TryFindStringNode(Literals.FeltNumber(), out var n) ? n : throw new JmaXmlParseException("FeltNumber ノードが存在しません"));
}
