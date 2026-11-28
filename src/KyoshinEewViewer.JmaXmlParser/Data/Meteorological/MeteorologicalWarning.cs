using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;

/// <summary>
/// 府県予報区・市町村などの地域区分ごとの気象警報・注意報。
/// </summary>
public struct MeteorologicalWarning(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	/// <summary>
	/// 対象とする地域区分の種類。
	/// </summary>
	public string Type => Node.TryFindStringAttribute(Literals.AttrType(), out var n) ? n : throw new JmaXmlParseException("type 属性が存在しません");

	/// <summary>
	/// 地域ごとの警報・注意報の発表状況。
	/// </summary>
	public IEnumerable<MeteorologicalItem> Items
		=> Node.Children.Where(c => c.Name == Literals.Item()).Select(c => new MeteorologicalItem(c));
}
