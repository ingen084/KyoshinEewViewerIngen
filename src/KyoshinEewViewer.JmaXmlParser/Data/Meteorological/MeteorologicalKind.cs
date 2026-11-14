using System;
using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;

/// <summary>
/// 警報・注意報や予想事項の種類と発表状況。
/// </summary>
public struct MeteorologicalKind(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	/// <summary>
	/// 種別名。発表警報・注意報がない場合などは null。
	/// </summary>
	public string? Name => Node.TryFindStringNode(Literals.Name(), out var n) ? n : null;

	/// <summary>
	/// 種別コード。先頭のゼロを保持する。存在しない場合は null。
	/// </summary>
	public string? Code => Node.TryFindStringNode(Literals.Code(), out var n) ? n : null;

	/// <summary>
	/// 発表、継続、解除、切替などの発表形態。存在しない場合は null。
	/// </summary>
	public string? Status => Node.TryFindStringNode(Literals.Status(), out var n) ? n : null;

	/// <summary>
	/// 階級名。存在しない場合は null。
	/// </summary>
	public string? ClassName => Node.TryFindStringNode("ClassName"u8, out var n) ? n : null;

	/// <summary>
	/// 警報等の状況。存在しない場合は null。
	/// </summary>
	public string? Condition => Node.TryFindStringNode(Literals.Condition(), out var n) ? n : null;

	/// <summary>
	/// 前回の発表内容。存在しない場合は null。
	/// </summary>
	public MeteorologicalNextKind? LastKind => Node.TryFindChild(Literals.LastKind(), out var n) ? new(n) : null;

	/// <summary>
	/// 今後の切替予定。
	/// </summary>
	public IEnumerable<MeteorologicalNextKind> NextKinds => Node.TryFindChild("NextKinds"u8, out var n)
		? n.Children.Where(c => c.Name == "NextKind"u8).Select(c => new MeteorologicalNextKind(c))
		: Enumerable.Empty<MeteorologicalNextKind>();

	/// <summary>
	/// 特記事項。
	/// </summary>
	public IEnumerable<string> AttentionNotes => Node.GetNotes("Attention"u8);

	/// <summary>
	/// 付加事項。
	/// </summary>
	public IEnumerable<string> AdditionNotes => Node.GetNotes("Addition"u8);

	/// <summary>
	/// 警報への切替に関する予告。存在しない場合は null。
	/// </summary>
	public WarningNotice? WarningNotice => Node.TryFindChild("WarningNotice"u8, out var n) ? new(n) : null;

	/// <summary>
	/// この種別の発表時刻。集約通報や時系列情報で使用する。
	/// </summary>
	public DateTimeOffset? DateTime => Node.TryFindDateTimeNode(Literals.DateTime(), out var n) ? n : null;

	/// <summary>
	/// 時刻の種類。存在しない場合は null。
	/// </summary>
	public string? DateTimeType => Node.TryFindChild(Literals.DateTime(), out var n) && n.TryFindStringAttribute(Literals.AttrType(), out var value) ? value : null;

	/// <summary>
	/// 危険度や量的予想事項。
	/// </summary>
	public IEnumerable<MeteorologicalInfoKindProperty> Properties
		=> Node.Children.Where(c => c.Name == Literals.Property()).Select(c => new MeteorologicalInfoKindProperty(c));
}
