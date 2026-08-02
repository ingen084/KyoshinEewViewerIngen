using System;
using System.Text.Json.Serialization;

#pragma warning disable CS8618 // JSON DTOs are populated by deserialization.

namespace KyoshinEewViewer.Services.ExtarnalPublishers.Axis.ApiModels.Message;

public class EarthquakeMessage
{
	public JmaXmlControl Control { get; set; }
	public EarthquakeBody Body { get; set; }
	public JmaXmlHead Head { get; set; }
	[JsonPropertyName("uuid_")]
	public string Uuid { get; set; }
}

public class JmaXmlControl
{
	public string Status { get; set; }
	// DateTime で受けるとオフセット付きの値がマシンのローカル時刻へ変換されてしまうため、DateTimeOffset で受ける
	public DateTimeOffset DateTime { get; set; }
	public string PublishingOffice { get; set; }
	public string EditorialOffice { get; set; }
	public string Title { get; set; }
}

public class EarthquakeBody
{
	public Intensity Intensity { get; set; }
	public Earthquake[] Earthquake { get; set; }
	public Comments Comments { get; set; }
}

public class Intensity
{
	public Observation Observation { get; set; }
}

public class Observation
{
	public string MaxInt { get; set; }
	public Pref[] Pref { get; set; }
}

public class Pref
{
	public string Code { get; set; }
	public string MaxInt { get; set; }
	public string Name { get; set; }
	public Area[] Area { get; set; }
}

public class Area
{
	public City[] City { get; set; }
	public string Code { get; set; }
	public string MaxInt { get; set; }
	public string Name { get; set; }
}

public class City
{
	public string Code { get; set; }
	public string MaxInt { get; set; }
	public string Name { get; set; }
	public Intensitystation[] IntensityStation { get; set; }
}

public class Intensitystation
{
	public string Int { get; set; }
	public string Code { get; set; }
	public string Name { get; set; }
}

public class Comments
{
	public Varcomment VarComment { get; set; }
	public Forecastcomment ForecastComment { get; set; }
}

public class Varcomment
{
	public string Text { get; set; }
	public string Code { get; set; }
	public string codeType { get; set; }
}

public class Forecastcomment
{
	public string Text { get; set; }
	public string Code { get; set; }
	[JsonPropertyName("codeType")]
	public string CodeType { get; set; }
}

public class Earthquake
{
	public PhysicalQuantity[] Magnitude { get; set; }
	public DateTimeOffset OriginTime { get; set; }
	public Hypocenter Hypocenter { get; set; }
	public DateTimeOffset ArrivalTime { get; set; }
}

public class Hypocenter
{
	public HypocenterArea Area { get; set; }
}

public class HypocenterArea
{
	public PhysicalQuantity[] Coordinate { get; set; }
	public Code Code { get; set; }
	public string Name { get; set; }
}

public class Code
{
	[JsonPropertyName("type_")]
	public string Type { get; set; }
	[JsonPropertyName("valueOf_")]
	public string ValueOf { get; set; }
}

public class PhysicalQuantity
{
	[JsonPropertyName("type_")]
	public string Type { get; set; }
	[JsonPropertyName("valueOf_")]
	public string ValueOf { get; set; }
	[JsonPropertyName("description")]
	public string Description { get; set; }
}

public class JmaXmlHead
{
	public string EventID { get; set; }
	public DateTimeOffset TargetDateTime { get; set; }
	public string InfoType { get; set; }
	public string Title { get; set; }
	public JmaXmlHeadline Headline { get; set; }
	public string InfoKindVersion { get; set; }
	public string InfoKind { get; set; }
	public DateTimeOffset ReportDateTime { get; set; }
	public string Serial { get; set; }
}

public class JmaXmlHeadline
{
	public string Text { get; set; }
	// public object[] Information { get; set; }
}
