using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;

/// <summary>
/// 対象地域と警報・注意報または量的予想事項の対応。
/// </summary>
public struct MeteorologicalItem(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	/// <summary>
	/// 気象警報・注意報や予想事項の種類。
	/// </summary>
	public IEnumerable<MeteorologicalKind> Kinds
		=> Node.Children.Where(c => c.Name == Literals.Kind()).Select(c => new MeteorologicalKind(c));

	/// <summary>
	/// 対象地域。存在しない場合は null。
	/// </summary>
	public MeteorologicalArea? Area => Node.TryFindChild(Literals.Area(), out var n) ? new(n) : null;

	/// <summary>
	/// 対象地域が複数記載される形式での地域一覧。
	/// </summary>
	public IEnumerable<MeteorologicalArea> Areas => Node.TryFindChild(Literals.Areas(), out var n)
		? n.Children.Where(c => c.Name == Literals.Area()).Select(c => new MeteorologicalArea(c))
		: Enumerable.Empty<MeteorologicalArea>();

	/// <summary>
	/// 複数地域のコード体系。存在しない場合は null。
	/// </summary>
	public string? AreaCodeType => Node.TryFindChild(Literals.Areas(), out var n) && n.TryFindStringAttribute(Literals.AttrCodeType(), out var value) ? value : null;

	/// <summary>
	/// 前回からの変更点。存在しない場合は null。
	/// </summary>
	public string? ChangeStatus => Node.TryFindStringNode("ChangeStatus"u8, out var n) ? n : null;

	/// <summary>
	/// 域内全域か一部かの区分。存在しない場合は null。
	/// </summary>
	public string? FullStatus => Node.TryFindStringNode("FullStatus"u8, out var n) ? n : null;

	/// <summary>
	/// 編集識別子。存在しない場合は null。
	/// </summary>
	public string? EditingMark => Node.TryFindStringNode("EditingMark"u8, out var n) ? n : null;

	/// <summary>
	/// 関連する情報。
	/// </summary>
	public IEnumerable<string> OtherReports
		=> Node.Children.Where(c => c.Name == "OtherReport"u8).Select(c => c.InnerText.ToString());
}
