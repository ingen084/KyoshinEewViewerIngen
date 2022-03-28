using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

public struct SvaPerPeriod
{
	private XmlNode Node { get; set; }

	public SvaPerPeriod(XmlNode node)
	{
		Node = node;
	}

	private string? unit = null;
	/// <summary>
	/// 単位
	/// </summary>
	public string Unit => unit ??= (Node.TryFindStringAttribute(Literals.AttrUnit(), out var n) ? n : throw new JmaXmlParseException("unit 属性が存在しません"));

	private int? periodicBand = null;
	/// <summary>
	/// 周期帯
	/// </summary>
	public int PeriodicBand => periodicBand ??= (Node.TryFindIntAttribute(Literals.PeriodicBand(), out var n) ? n : throw new JmaXmlParseException("PeriodicBand 属性が存在しません"));

	private string? periodUnit = null;
	/// <summary>
	/// 周期帯(単位)
	/// </summary>
	public string PeriodUnit => periodUnit ??= (Node.TryFindStringAttribute(Literals.PeriodUnit(), out var n) ? n : throw new JmaXmlParseException("PeriodUnit 属性が存在しません"));

	private float? value = null;
	/// <summary>
	/// 加速度
	/// </summary>
	public float Value => value ??= Node.InnerText.TryToFloat32(out var v) ? v : throw new JmaXmlParseException("値がfloatとしてパースできません");
}
