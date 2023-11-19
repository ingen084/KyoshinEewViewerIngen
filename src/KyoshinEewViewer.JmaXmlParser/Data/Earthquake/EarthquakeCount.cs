using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

public struct EarthquakeCount(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	/// <summary>
	/// 地震回数（期間別）
	/// </summary>
	public IEnumerable<EarthquakeCountItem> Cities
		=> Node.Children.Where(c => c.Name == Literals.Item()).Select(c => new EarthquakeCountItem(c));
}
