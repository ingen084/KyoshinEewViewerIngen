using System;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

/// <summary>
/// 指定期間・規模に対する余震発生確率。
/// </summary>
public struct AftershockItem(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	private DateTimeOffset? _startTime = null;
	/// <summary>
	/// 対象期間の開始時刻。
	/// </summary>
	public DateTimeOffset StartTime => _startTime ??= (Node.TryFindDateTimeNode(Literals.StartTime(), out var n) ? n : throw new JmaXmlParseException("StartTime ノードが存在しません"));

	private DateTimeOffset? _endTime = null;
	/// <summary>
	/// 対象期間の終了時刻。
	/// </summary>
	public DateTimeOffset EndTime => _endTime ??= (Node.TryFindDateTimeNode(Literals.EndTime(), out var n) ? n : throw new JmaXmlParseException("EndTime ノードが存在しません"));

	private PhysicalQuantity? _probabilityOfAftershock = null;
	/// <summary>
	/// 余震発生確率。
	/// </summary>
	public PhysicalQuantity ProbabilityOfAftershock => _probabilityOfAftershock ??= (Node.TryFindElementBasis("ProbabilityOfAftershock"u8, out var n) ? new(n) : throw new JmaXmlParseException("ProbabilityOfAftershock ノードが存在しません"));

	private PhysicalQuantity? _targetMagnitude = null;
	/// <summary>
	/// 対象となる余震のマグニチュード。
	/// </summary>
	public PhysicalQuantity TargetMagnitude => _targetMagnitude ??= (Node.TryFindChild("TargetMagnitude"u8, out var n) ? new(n) : throw new JmaXmlParseException("TargetMagnitude ノードが存在しません"));

	private string? _text = null;
	/// <summary>
	/// 補足情報。存在しない場合は null。
	/// </summary>
	public string? Text => _text ??= (Node.TryFindStringNode(Literals.Text(), out var n) ? n : null);
}
