using System;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Tsunami;

public struct TsunamiStation(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	private string? _name = null;
	/// <summary>
	/// 観測点名
	/// </summary>
	public string Name => _name ??= (Node.TryFindStringNode(Literals.Name(), out var n) ? n : throw new JmaXmlParseException("Name ノードが存在しません"));

	private int? _code = null;
	/// <summary>
	/// 地域コード
	/// </summary>
	public int Code => _code ??= (Node.TryFindIntNode(Literals.Code(), out var n) ? n : throw new JmaXmlParseException("Code ノードが存在しません"));

	private string? _sensor = null;
	/// <summary>
	/// 観測機器の名称（ＧＮＳＳ波浪計、水圧計など）。存在しない場合は null。
	/// </summary>
	public string? Sensor => _sensor ??= (Node.TryFindStringNode("Sensor"u8, out var n) ? n : null);

	private CurrentHeight? _currentHeight = null;
	/// <summary>
	/// 津波の現在の高さ。存在しない場合は null。
	/// </summary>
	public CurrentHeight? CurrentHeight => _currentHeight ??= (Node.TryFindChild("CurrentHeight"u8, out var n) ? new(n) : null);

	private DateTimeOffset? _highTideDateTime = null;
	/// <summary>
	/// 満潮時刻(Forecast のみ)
	/// </summary>
	public DateTimeOffset? HighTideDateTime => _highTideDateTime ??= (Node.TryFindDateTimeNode("HighTideDateTime"u8, out var n) ? n : null);

	private FirstHeight? _firstHeight = null;
	/// <summary>
	/// Observation: 観測した津波の第１波<br/>
	/// Forecast: 津波の到達予想時刻
	/// </summary>
	public FirstHeight? FirstHeight => _firstHeight ??= (Node.TryFindChild("FirstHeight"u8, out var n) ? new(n) : null);

	private MaxHeight? _maxHeight = null;
	/// <summary>
	/// 観測したこれまでの最大波(Observation のみ)
	/// </summary>
	public MaxHeight? MaxHeight => _maxHeight ??= (Node.TryFindChild("MaxHeight"u8, out var n) ? new(n) : null);
}
