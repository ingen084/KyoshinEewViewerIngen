using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;

/// <summary>
/// 種類ごとの一連の気象情報（MeteorologicalInfos 要素）。
/// </summary>
public struct MeteorologicalInfoGroup(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	private string? _type = null;
	/// <summary>
	/// 一連の気象情報の種類。
	/// </summary>
	public string Type => _type ??= (Node.TryFindStringAttribute(Literals.AttrType(), out var n) ? n : throw new JmaXmlParseException("type 属性が存在しません"));

	/// <summary>
	/// 予報や観測の時刻ごとの情報。
	/// </summary>
	public IEnumerable<MeteorologicalInfo> MeteorologicalInfos
		=> Node.Children.Where(c => c.Name == Literals.MeteorologicalInfo()).Select(c => new MeteorologicalInfo(c));
}
