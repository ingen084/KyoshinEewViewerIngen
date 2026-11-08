using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;

/// <summary>
/// 高潮警報等に関連する水位観測所と基準値。
/// </summary>
public struct HydrometricStationPart(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	/// <summary>
	/// 水位観測所の名称と位置。
	/// </summary>
	public MeteorologicalArea Area => Node.TryFindChild("Area"u8, out var n) ? new(n) : throw new JmaXmlParseException("Area ノードが存在しません");

	/// <summary>
	/// 担当区間。
	/// </summary>
	public IEnumerable<string> ChargeSections => Node.Children.Where(c => c.Name == "ChargeSection"u8).Select(c => c.InnerText.ToString());

	/// <summary>
	/// 基準水位や流量。Quantities から取得する。
	/// </summary>
	public IEnumerable<MeteorologicalForecastPart> Criteria
		=> Node.Children.Where(c => c.Name == "Criteria"u8).Select(c => new MeteorologicalForecastPart(c));

	/// <summary>
	/// 補足情報。存在しない場合は null。
	/// </summary>
	public string? Remark => Node.TryFindStringNode("Remark"u8, out var n) ? n : null;
}
