using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Earthquake;

public struct SvaPerPeriod(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	private string? _unit = null;
	/// <summary>
	/// 単位
	/// </summary>
	public string Unit => _unit ??= (Node.TryFindStringAttribute(Literals.AttrUnit(), out var n) ? n : throw new JmaXmlParseException("unit 属性が存在しません"));

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

	private float? _value = null;
	/// <summary>
	/// 周期帯における絶対速度応答スペクトルの最大値。
	/// </summary>
	public float Value => _value ??= Node.InnerText.TryToFloat32(out var v) ? v : throw new JmaXmlParseException("値がfloatとしてパースできません");
}
