using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Tsunami;

public struct TsunamiForecastItem(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	private Area? _area = null;
	/// <summary>
	/// 津波予報区
	/// </summary>
	public Area Area => _area ??= (Node.TryFindChild(Literals.Area(), out var n) ? new(n) : throw new JmaXmlParseException("Area ノードが存在しません"));

	private Category? _category = null;
	/// <summary>
	/// 津波警報等の種類
	/// </summary>
	public Category Category => _category ??= (Node.TryFindChild(Literals.Category(), out var n) ? new(n) : throw new JmaXmlParseException("Category ノードが存在しません"));

	private FirstHeight? _firstHeight = null;
	/// <summary>
	/// 津波の到達予想時刻（津波予報区）<br/>
	/// 津波警報・注意報解除時/津波予報発表時には出現しない
	/// </summary>
	public FirstHeight? FirstHeight => _firstHeight ??= (Node.TryFindChild("FirstHeight"u8, out var n) ? new(n) : null);

	private MaxHeight? _maxHeight = null;
	/// <summary>
	/// 予想される津波の高さ（津波予報区）<br/>
	/// 津波警報・注意報解除時/津波予報発表時には出現しない
	/// </summary>
	public MaxHeight? MaxHeight => _maxHeight ??= (Node.TryFindChild("MaxHeight"u8, out var n) ? new(n) : null);
}
