using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

public struct LgIntPerPeriod
{
	private XmlNode Node { get; set; }

	public LgIntPerPeriod(XmlNode node)
	{
		Node = node;
	}

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

	private string? value = null;
	/// <summary>
	/// 長周期地震動階級
	/// </summary>
	public string Value => value ??= Node.InnerText.ToString();
}
