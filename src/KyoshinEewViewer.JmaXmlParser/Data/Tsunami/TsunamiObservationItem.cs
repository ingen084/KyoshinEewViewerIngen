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
	/// 津波警報等の種類。存在しない場合は null。
	/// </summary>
	public Category? Category => Node.TryFindChild(Literals.Category(), out var n) ? new(n) : null;

	/// <summary>
	/// 第１波の情報。存在しない場合は null。
	/// </summary>
	public FirstHeight? FirstHeight => Node.TryFindChild("FirstHeight"u8, out var n) ? new(n) : null;

	/// <summary>
	/// 最大波の情報。存在しない場合は null。
	/// </summary>
	public MaxHeight? MaxHeight => Node.TryFindChild("MaxHeight"u8, out var n) ? new(n) : null;

	/// <summary>
	/// 対象期間（ISO 8601 の期間表現）。存在しない場合は null。
	/// </summary>
	public string? Duration => Node.TryFindStringNode("Duration"u8, out var n) ? n : null;

	/// <summary>
	/// 潮位観測点
	/// </summary>
	public IEnumerable<TsunamiStation> Stations
		=> Node.Children.Where(c => c.Name == Literals.Station()).Select(c => new TsunamiStation(c));
}
