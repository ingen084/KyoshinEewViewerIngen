using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;

/// <summary>
/// 数値と区分の境界値で表す風速階級。危険度の名称・コードとは別の形式。
/// </summary>
public struct WindSpeedLevel(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	/// <summary>
	/// 要素の種類（風速階級）。
	/// </summary>
	public string Type => Node.TryFindStringAttribute(Literals.AttrType(), out var n) ? n : throw new JmaXmlParseException("type 属性が存在しません");

	/// <summary>
	/// 時刻の参照番号。
	/// </summary>
	public int RefId => Node.TryFindIntAttribute(Literals.AttrRefId(), out var n) ? n : throw new JmaXmlParseException("refID 属性が存在しません");

	/// <summary>
	/// 区分の境界値（空白区切りの原文）。
	/// </summary>
	public string Range => Node.TryFindStringAttribute("range"u8, out var n) ? n : throw new JmaXmlParseException("range 属性が存在しません");

	/// <summary>
	/// 文字列表現。存在しない場合は null。
	/// </summary>
	public string? Description => Node.TryFindStringAttribute(Literals.AttrDescription(), out var n) ? n : null;

	/// <summary>
	/// 風速階級の値。不明時は空文字や NaN。
	/// </summary>
	public string Value => Node.InnerText.ToString();

	public bool TryGetFloatValue(out float value) => Node.InnerText.TryToFloat32(out value);
}
