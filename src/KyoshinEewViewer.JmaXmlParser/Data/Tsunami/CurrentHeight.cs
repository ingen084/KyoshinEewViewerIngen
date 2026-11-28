using System;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Tsunami;

/// <summary>
/// 津波の現在の高さに関する観測値。
/// </summary>
public struct CurrentHeight(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	private DateTimeOffset? _startTime = null;
	/// <summary>
	/// 観測期間の開始時刻。存在しない場合は null。
	/// </summary>
	public DateTimeOffset? StartTime => _startTime ??= (Node.TryFindDateTimeNode(Literals.StartTime(), out var n) ? n : null);

	private DateTimeOffset? _endTime = null;
	/// <summary>
	/// 観測期間の終了時刻。存在しない場合は null。
	/// </summary>
	public DateTimeOffset? EndTime => _endTime ??= (Node.TryFindDateTimeNode(Literals.EndTime(), out var n) ? n : null);

	private string? _condition = null;
	/// <summary>
	/// 観測値の状態。存在しない場合は null。
	/// </summary>
	public string? Condition => _condition ??= (Node.TryFindStringNode(Literals.Condition(), out var n) ? n : null);

	private PhysicalQuantity? _tsunamiHeight = null;
	/// <summary>
	/// 津波の高さ。存在しない場合は null。
	/// </summary>
	public PhysicalQuantity? TsunamiHeight => _tsunamiHeight ??= (Node.TryFindElementBasis("TsunamiHeight"u8, out var n) ? new(n) : null);
}
