using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;

/// <summary>
/// 警戒・注意事項や量的予想事項の時系列。
/// </summary>
public struct MeteorologicalTimeSeriesInfo(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	/// <summary>
	/// 時刻定義。TimeId を危険度・物理量の RefId と対応付ける。
	/// </summary>
	public IEnumerable<MeteorologicalTimeDefine> TimeDefines => Node.TryFindChild("TimeDefines"u8, out var n)
		? n.Children.Where(c => c.Name == "TimeDefine"u8).Select(c => new MeteorologicalTimeDefine(c))
		: Enumerable.Empty<MeteorologicalTimeDefine>();

	/// <summary>
	/// 地域ごとの時系列。
	/// </summary>
	public IEnumerable<MeteorologicalItem> Items
		=> Node.Children.Where(c => c.Name == "Item"u8).Select(c => new MeteorologicalItem(c));
}
