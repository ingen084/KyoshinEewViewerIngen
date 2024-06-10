using System;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Tsunami;
public struct FirstHeight(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	private DateTimeOffset? _arrivalTime = null;
	/// <summary>
	/// 当該津波予報区への第1波の到達予想時刻
	/// </summary>
	public DateTimeOffset? ArrivalTime => _arrivalTime ??= (Node.TryFindDateTimeNode(Literals.ArrivalTime(), out var n) ? n : null);

	private string? _condition = null;
	/// <summary>
	/// 水位が上昇中の場合 "上昇中"
	/// </summary>
	public string? Condition => _condition ??= (Node.TryFindStringNode(Literals.Condition(), out var n) ? n : null);

	private string? _initial = null;
	/// <summary>
	/// 観測した津波の極性<br/>
	/// 押し or 引き
	/// </summary>
	public string? Initial => _initial ??= (Node.TryFindStringNode(Literals.Initial(), out var n) ? n : null);

	private string? _revise = null;
	/// <summary>
	/// 新たに出現した場合は 追加<br/>
	/// 既出かつ更新された場合は 更新
	/// </summary>
	public string? Revise => _revise ??= (Node.TryFindStringNode(Literals.Revise(), out var n) ? n : null);
}
