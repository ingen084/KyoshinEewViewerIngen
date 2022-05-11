using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

public struct Sva
{
	private XmlNode Node { get; set; }

	public Sva(XmlNode node)
	{
		Node = node;
	}

	private string? unit = null;
	/// <summary>
	/// 単位
	/// </summary>
	public string Unit => unit ??= (Node.TryFindStringAttribute(Literals.AttrUnit(), out var n) ? n : throw new JmaXmlParseException("unit 属性が存在しません"));

	private float? value = null;
	/// <summary>
	/// 加速度
	/// </summary>
	public float Value => value ??= Node.InnerText.TryToFloat32(out var v) ? v : throw new JmaXmlParseException("値がfloatとしてパースできません");
}