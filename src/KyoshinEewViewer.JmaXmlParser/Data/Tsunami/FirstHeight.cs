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
	/// 第１波の到達予想時刻までに時間的な猶予が無い場合は、 "ただちに津波来襲と予測"<br/>
	/// 当該津波予報区内の潮位観測点で第１波が観測された場合は、 ArrivalTime に代わってそれぞれ、"津波到達中と推測"、"第１波の到達を確認"
	/// </summary>
	public string? Condition => _condition ??= (Node.TryFindStringNode(Literals.Condition(), out var n) ? n : null);

	private string? _revise = null;
	/// <summary>
	/// 新たに出現した場合は 追加<br/>
	/// 既出かつ更新された場合は 更新
	/// </summary>
	public string? Revise => _revise ??= (Node.TryFindStringNode(Literals.Revise(), out var n) ? n : null);
}
