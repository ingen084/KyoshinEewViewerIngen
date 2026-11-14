using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

public struct LgIntPerPeriod(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	private int? _periodicBand = null;
	/// <summary>
	/// 周期帯。PeriodicBand 属性が存在しない場合は null。
	/// </summary>
	public int? PeriodicBand => _periodicBand ??= (Node.TryFindIntAttribute(Literals.PeriodicBand(), out var n) ? n : null);

	private string? _periodUnit = null;
	/// <summary>
	/// 周期帯の単位。PeriodUnit 属性が存在しない場合は null。
	/// </summary>
	public string? PeriodUnit => _periodUnit ??= (Node.TryFindStringAttribute(Literals.PeriodUnit(), out var n) ? n : null);

	private float? _period = null;
	/// <summary>
	/// 周期。Period 属性が存在しない場合は null。
	/// </summary>
	public float? Period => _period ??= (Node.TryFindFloatAttribute("Period"u8, out var n) ? n : null);

	private string? _value = null;
	/// <summary>
	/// 長周期地震動階級
	/// </summary>
	public string Value => _value ??= Node.InnerText.ToString();
}
