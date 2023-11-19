using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

public struct LgIntPerPeriod(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	private int? _periodicBand = null;
	/// <summary>
	/// 周期帯
	/// </summary>
	public int PeriodicBand => _periodicBand ??= (Node.TryFindIntAttribute(Literals.PeriodicBand(), out var n) ? n : throw new JmaXmlParseException("PeriodicBand 属性が存在しません"));

	private string? _periodUnit = null;
	/// <summary>
	/// 周期帯(単位)
	/// </summary>
	public string PeriodUnit => _periodUnit ??= (Node.TryFindStringAttribute(Literals.PeriodUnit(), out var n) ? n : throw new JmaXmlParseException("PeriodUnit 属性が存在しません"));

	private string? _value = null;
	/// <summary>
	/// 長周期地震動階級
	/// </summary>
	public string Value => _value ??= Node.InnerText.ToString();
}
