using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;
public struct MeteorologicalBody(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	/// <summary>
	/// 地域区分ごとの気象警報・注意報。旧形式・Ｒ０６形式・集約通報に共通。
	/// </summary>
	public IEnumerable<MeteorologicalWarning> Warnings
		=> Node.Children.Where(c => c.Name == "Warning"u8).Select(c => new MeteorologicalWarning(c));

	/// <summary>
	/// 対象地域。存在しない場合は null。
	/// </summary>
	public MeteorologicalArea? TargetArea => Node.TryFindChild("TargetArea"u8, out var n) ? new(n) : null;

	/// <summary>
	/// 付加的な解説。存在しない場合は null。
	/// </summary>
	public MeteorologicalComment? Comment => Node.TryFindChild("Comment"u8, out var n) ? new(n) : null;

	/// <summary>
	/// 発表官署の情報。存在しない場合は空。
	/// </summary>
	public IEnumerable<MeteorologicalOffice> Offices => Node.TryFindChild("OfficeInfo"u8, out var n)
		? n.Children.Where(c => c.Name == "Office"u8).Select(c => new MeteorologicalOffice(c))
		: Enumerable.Empty<MeteorologicalOffice>();

	/// <summary>
	/// 高潮予報区間や水位観測所の補足情報。存在しない場合は空。
	/// </summary>
	public IEnumerable<TidalWarningAddition> TidalWarningAdditions => Node.TryFindChild("AdditionalInfo"u8, out var n)
		? n.Children.Where(c => c.Name == "TidalWarningAddition"u8).Select(c => new TidalWarningAddition(c))
		: Enumerable.Empty<TidalWarningAddition>();

	/// <summary>
	/// 種類ごとの一連の気象情報の全件。
	/// </summary>
	public IEnumerable<MeteorologicalInfoGroup> MeteorologicalInfoGroups
		=> Node.Children.Where(c => c.Name == Literals.MeteorologicalInfos()).Select(c => new MeteorologicalInfoGroup(c));

	/// <summary>
	/// 気象情報についての注意事項。
	/// </summary>
	public IEnumerable<string> Notices
		=> Node.Children.Where(c => c.Name == "Notice"u8).Select(c => c.InnerText.ToString());

	private string? _type = null;
	/// <summary>
	/// 一連の気象情報の種類（先頭の MeteorologicalInfos 要素）。
	/// </summary>
	public string Type
	{
		get {
			if (_type != null)
				return _type;
			if (!Node.TryFindChild(Literals.MeteorologicalInfos(), out var n))
				throw new JmaXmlParseException("MeteorologicalInfos ノードが存在しません");
			if (!n.TryFindStringAttribute(Literals.AttrType(), out var r))
				throw new JmaXmlParseException("type 属性が存在しません");
			return _type = r;
		}
	}

	/// <summary>
	/// 各気象情報の予報や観測事項（先頭の MeteorologicalInfos 要素）。
	/// </summary>
	public IEnumerable<MeteorologicalInfo> MeteorologicalInfos
	{
		get {
			if (!Node.TryFindChild(Literals.MeteorologicalInfos(), out var n))
				return Enumerable.Empty<MeteorologicalInfo>();
			return n.Children.Where(c => c.Name == Literals.MeteorologicalInfo()).Select(c => new MeteorologicalInfo(c));
		}
	}
}
