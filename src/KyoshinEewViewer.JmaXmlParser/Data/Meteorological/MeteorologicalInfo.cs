using System;
using System.Collections.Generic;
using System.Linq;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;
public struct MeteorologicalInfo(XmlNode node)
{
	private XmlNode Node { get; set; } = node;

	private string? _type = null;
	/// <summary>
	/// 気象情報の種別。存在しない場合は null。
	/// </summary>
	public string? Type => _type ??= (Node.TryFindStringAttribute(Literals.AttrType(), out var n) ? n : null);

	private string? _name = null;
	/// <summary>
	/// 予報や観測の対象期間名。存在しない場合は null。
	/// </summary>
	public string? Name => _name ??= (Node.TryFindStringNode(Literals.Name(), out var n) ? n : null);

	private string? _duration = null;
	/// <summary>
	/// 予報や観測の対象期間（ISO 8601 の期間表現）。存在しない場合は null。
	/// </summary>
	public string? Duration => _duration ??= (Node.TryFindStringNode("Duration"u8, out var n) ? n : null);

	private DateTimeOffset? _dateTime = null;
	/// <summary>
	/// 予報や観測時刻
	/// </summary>
	public DateTimeOffset DateTime => _dateTime ??= (Node.TryFindDateTimeNode(Literals.DateTime(), out var n) ? n : throw new JmaXmlParseException("DateTime ノードが存在しません"));

	private string? _dateTimeType = null;
	/// <summary>
	/// 予報や観測時刻の種類。type 属性が存在しない場合は null。
	/// </summary>
	public string? DateTimeType
	{
		get {
			if (_dateTimeType != null)
				return _dateTimeType;
			if (!Node.TryFindChild(Literals.DateTime(), out var n))
				throw new JmaXmlParseException("DateTime ノードが存在しません");
			return _dateTimeType = n.TryFindStringAttribute(Literals.AttrType(), out var r) ? r : null;
		}
	}

	/// <summary>
	/// 時刻の有効桁。存在しない場合は null。
	/// </summary>
	public string? DateTimeSignificant => GetDateTimeAttribute("significant"u8);

	/// <summary>
	/// 時刻の精度。存在しない場合は null。
	/// </summary>
	public string? DateTimePrecision => GetDateTimeAttribute("precision"u8);

	/// <summary>
	/// 時刻のあいまいさ。存在しない場合は null。
	/// </summary>
	public string? DateTimeDubious => GetDateTimeAttribute("dubious"u8);

	/// <summary>
	/// 時刻の文字列表現。存在しない場合は null。
	/// </summary>
	public string? DateTimeDescription => GetDateTimeAttribute(Literals.AttrDescription());

	private string? GetDateTimeAttribute(ReadOnlySpan<byte> name)
	{
		if (!Node.TryFindChild(Literals.DateTime(), out var n))
			throw new JmaXmlParseException("DateTime ノードが存在しません");
		return n.TryFindStringAttribute(name, out var value) ? value : null;
	}

	/// <summary>
	/// 各気象情報の予報や観測事項
	/// </summary>
	public IEnumerable<MeteorologicalInfoKindProperty> MeteorologicalInfoKindProperties
		=> Node.Children.Where(c => c.Name == Literals.Item())
			.SelectMany(c => c.Children.Where(k => k.Name == Literals.Kind()))
			.SelectMany(c => c.Children.Where(p => p.Name == Literals.Property()))
			.Select(c => new MeteorologicalInfoKindProperty(c));

	/// <summary>
	/// 熱帯低気圧の強風域･暴風域の情報
	/// </summary>
	public IEnumerable<TyphoonCircle> TyphoonCircles
		=> Node.Children.Where(c => c.Name == Literals.Item())
			.SelectMany(c => c.Children.Where(a => a.Name == Literals.Area()))
			.SelectMany(c => c.Children.Where(a => a.IsElementBasis("Circle"u8)))
			.Select(c => new TyphoonCircle(c));
}
