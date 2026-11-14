using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Tsunami;

/// <summary>
/// 沿岸地域ごとの津波到達時刻・高さの推定値。
/// </summary>
public struct TsunamiEstimationItem(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	private Area? _area = null;
	/// <summary>
	/// 対象の沿岸地域。
	/// </summary>
	public Area Area => _area ??= (Node.TryFindChild(Literals.Area(), out var n) ? new(n) : throw new JmaXmlParseException("Area ノードが存在しません"));

	private FirstHeight? _firstHeight = null;
	/// <summary>
	/// 津波到達時刻の推定値。存在しない場合は null。
	/// </summary>
	public FirstHeight? FirstHeight => _firstHeight ??= (Node.TryFindChild("FirstHeight"u8, out var n) ? new(n) : null);

	private MaxHeight? _maxHeight = null;
	/// <summary>
	/// 最大波の到達時刻と高さの推定値。存在しない場合は null。
	/// </summary>
	public MaxHeight? MaxHeight => _maxHeight ??= (Node.TryFindChild("MaxHeight"u8, out var n) ? new(n) : null);

	/// <summary>
	/// 津波警報等の種類。存在しない場合は null。
	/// </summary>
	public Category? Category => Node.TryFindChild(Literals.Category(), out var n) ? new(n) : null;

	/// <summary>
	/// 対象期間（ISO 8601 の期間表現）。存在しない場合は null。
	/// </summary>
	public string? Duration => Node.TryFindStringNode("Duration"u8, out var n) ? n : null;

	/// <summary>
	/// 潮位観測点。存在しない場合は空。
	/// </summary>
	public IEnumerable<TsunamiStation> Stations
		=> Node.Children.Where(c => c.Name == Literals.Station()).Select(c => new TsunamiStation(c));
}
