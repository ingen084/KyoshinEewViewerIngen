using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data;

public struct Coordinate
{
	private XmlNode Node { get; set; }

	public Coordinate(XmlNode node)
	{
		Node = node;
	}

	private string? description = null;
	/// <summary>
	/// 文字列表現<br/>
	/// 例: 北緯３９．０度 東経１４０．９度 深さ １０ｋｍ、震源要素不明
	/// </summary>
	public string Description => description ??= (Node.TryFindStringAttribute(Literals.AttrDescription(), out var n) ? n : throw new JmaXmlParseException("description 属性が存在しません"));

	private string? datum = null;
	/// <summary>
	/// 座標系 そもそも座標が不明の場合は null<br/>
	/// 例: 日本測地系
	/// </summary>
	public string? Datum => datum ??= (Node.TryFindStringAttribute(Literals.AttrDatum(), out var n) ? n : null);

	private string? value = null;
	/// <summary>
	/// ISO6709 に準拠した座標表記<br/>
	/// 座標が不明な場合空になる<br/>
	/// 例: +39.0+140.9-10000/
	/// </summary>
	public string? Value => value ??= Node.InnerText.ToString();
}
