using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;

/// <summary>
/// 高潮予報区間に関する補足情報。
/// </summary>
public struct TidalAreaPart(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	/// <summary>
	/// 高潮予報区間。
	/// </summary>
	public MeteorologicalArea Area => Node.TryFindChild("Area"u8, out var n) ? new(n) : throw new JmaXmlParseException("Area ノードが存在しません");

	/// <summary>
	/// 担当区間。
	/// </summary>
	public IEnumerable<string> ChargeSections => Node.Children.Where(c => c.Name == "ChargeSection"u8).Select(c => c.InnerText.ToString());
}
