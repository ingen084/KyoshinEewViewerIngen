using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

/// <summary>
/// 震源情報
/// </summary>
public struct Hypocenter
{
	private XmlNode Node { get; set; }

	public Hypocenter(XmlNode node)
	{
		Node = node;
	}

	private HypocenterArea? _area = null;
	/// <summary>
	/// 震源位置
	/// </summary>
	public HypocenterArea Area => _area ??= (Node.TryFindChild(Literals.Area(), out var n) ? new HypocenterArea(n) : throw new JmaXmlParseException("Area ノードが存在しません"));

	private HypocenterAccuracy? _accuracy = null;
	/// <summary>
	/// 精度情報
	/// </summary>
	public HypocenterAccuracy Accuracy => _accuracy ??= (Node.TryFindChild(Literals.Accuracy(), out var n) ? new HypocenterAccuracy(n) : throw new JmaXmlParseException("Accuracy ノードが存在しません"));
}
