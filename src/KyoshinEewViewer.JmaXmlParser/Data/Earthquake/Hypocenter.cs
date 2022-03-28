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

	private HypocenterArea? area = null;
	/// <summary>
	/// 震源位置
	/// </summary>
	public HypocenterArea Area => area ??= (Node.TryFindChild(Literals.Area(), out var n) ? new(n) : throw new JmaXmlParseException("Area ノードが存在しません"));

	private HypocenterAccuracy? accuracy = null;
	/// <summary>
	/// 精度情報
	/// </summary>
	public HypocenterAccuracy Accuracy => accuracy ??= (Node.TryFindChild(Literals.Accuracy(), out var n) ? new(n) : throw new JmaXmlParseException("Accuracy ノードが存在しません"));

	private PhysicalQuantity? magnitude = null;
	/// <summary>
	/// 地震のマグニチュードの値
	/// <para><seealso cref="PhysicalQuantity.Type"/> にマグニチュードの種別(Mj,Mw等)<br/>
	/// <seealso cref="PhysicalQuantity.Description"/> に文字列表現</para>
	/// <para>マグニチュードが不明の場合、 <seealso cref="PhysicalQuantity.Condition"/> が "不明" となり値は "NaN" になる</para>
	/// <para>「仮定震源要素」の場合、値は "1.0" 固定</para>
	/// </summary>
	public PhysicalQuantity Magnitude => magnitude ??= (Node.TryFindChild(Literals.Magnitude(), out var n) ? new(n) : throw new JmaXmlParseException("Magnitude ノードが存在しません"));
}
