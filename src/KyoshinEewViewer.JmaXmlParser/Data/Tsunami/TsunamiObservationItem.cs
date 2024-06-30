using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Tsunami;

public struct TsunamiObservationItem(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	private Area? _area = null;
	/// <summary>
	/// 津波予報区
	/// </summary>
	public Area Area => _area ??= (Node.TryFindChild(Literals.Area(), out var n) ? new(n) : throw new JmaXmlParseException("Area ノードが存在しません"));

	/// <summary>
	/// 潮位観測点
	/// </summary>
	public IEnumerable<TsunamiStation> Stations
		=> Node.Children.Where(c => c.Name == Literals.Station()).Select(c => new TsunamiStation(c));
}
