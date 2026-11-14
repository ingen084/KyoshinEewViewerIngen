using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;

/// <summary>
/// 高潮予報区間の担当区間と水位観測所の補足情報。
/// </summary>
public struct TidalWarningAddition(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	/// <summary>
	/// 対象となる高潮予報区間。
	/// </summary>
	public TidalAreaPart TidalAreaPart => Node.TryFindChild("TidalAreaPart"u8, out var n) ? new(n) : throw new JmaXmlParseException("TidalAreaPart ノードが存在しません");

	/// <summary>
	/// 水位観測所ごとの基準値。
	/// </summary>
	public IEnumerable<HydrometricStationPart> HydrometricStationParts
		=> Node.Children.Where(c => c.Name == "HydrometricStationPart"u8).Select(c => new HydrometricStationPart(c));
}
