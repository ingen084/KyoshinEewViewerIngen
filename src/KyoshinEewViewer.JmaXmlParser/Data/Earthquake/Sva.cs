using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

public struct Sva(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	private string? _unit = null;
	/// <summary>
	/// 単位
	/// </summary>
	public string Unit => _unit ??= (Node.TryFindStringAttribute(Literals.AttrUnit(), out var n) ? n : throw new JmaXmlParseException("unit 属性が存在しません"));

	private float? _value = null;
	/// <summary>
	/// 加速度
	/// </summary>
	public float Value => _value ??= Node.InnerText.TryToFloat32(out var v) ? v : throw new JmaXmlParseException("値がfloatとしてパースできません");
}
