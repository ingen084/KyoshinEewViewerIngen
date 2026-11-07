using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

/// <summary>
/// 震源情報
/// </summary>
public struct Hypocenter(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	private HypocenterArea? _area = null;
	/// <summary>
	/// 震源位置
	/// </summary>
	public HypocenterArea Area => _area ??= (Node.TryFindChild(Literals.Area(), out var n) ? new(n) : throw new JmaXmlParseException("Area ノードが存在しません"));

	private HypocenterAccuracy? _accuracy = null;
	/// <summary>
	/// 精度情報。省略されている場合は例外。省略を許容する取得には TryGetAccuracy を使用する。
	/// </summary>
	public HypocenterAccuracy Accuracy => _accuracy ??= (Node.TryFindChild(Literals.Accuracy(), out var n) ? new(n) : throw new JmaXmlParseException("Accuracy ノードが存在しません"));

	/// <summary>
	/// 省略可能な精度情報を取得する。存在しない場合は false。
	/// </summary>
	public bool TryGetAccuracy(out HypocenterAccuracy accuracy)
	{
		accuracy = default;
		if (!Node.TryFindChild(Literals.Accuracy(), out var n))
			return false;
		accuracy = new(n);
		return true;
	}

	private string? _source = null;
	/// <summary>
	/// 震源決定機関（PTWC、USGS など）。存在しない場合は null。
	/// </summary>
	public string? Source => _source ??= (Node.TryFindStringNode(Literals.Source(), out var n) ? n : null);
}
