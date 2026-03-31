using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Models;

/// <summary>
/// EQMonitor Backend の EventMessage JSON をデシリアライズするモデル。
/// リプレイファイル内の EqMonitorEewReplayData.Json を変換する際に使用。
/// </summary>
public class EqMonitorEventMessage
{
	[JsonPropertyName("eventId")]
	public string EventId { get; set; } = "";

	[JsonPropertyName("type")]
	public string Type { get; set; } = "";

	[JsonPropertyName("serialNo")]
	public int SerialNo { get; set; }

	[JsonPropertyName("reportTime")]
	public string ReportTime { get; set; } = "";

	[JsonPropertyName("originTime")]
	public string? OriginTime { get; set; }

	[JsonPropertyName("arrivalTime")]
	public string? ArrivalTime { get; set; }

	[JsonPropertyName("maxIntensity")]
	public string? MaxIntensity { get; set; }

	[JsonPropertyName("magnitude")]
	public double? Magnitude { get; set; }

	[JsonPropertyName("isWarning")]
	public bool? IsWarning { get; set; }

	[JsonPropertyName("isLastInfo")]
	public bool? IsLastInfo { get; set; }

	[JsonPropertyName("isCancel")]
	public bool? IsCancel { get; set; }

	[JsonPropertyName("hypocenter")]
	public EqMonitorHypocenter? Hypocenter { get; set; }

	[JsonPropertyName("regions")]
	public List<EqMonitorRegion> Regions { get; set; } = [];
}

public class EqMonitorHypocenter
{
	[JsonPropertyName("latitude")]
	public double Latitude { get; set; }

	[JsonPropertyName("longitude")]
	public double Longitude { get; set; }

	[JsonPropertyName("depth")]
	public int Depth { get; set; }

	[JsonPropertyName("name")]
	public string? Name { get; set; }
}

public class EqMonitorRegion
{
	[JsonPropertyName("code")]
	public string Code { get; set; } = "";

	[JsonPropertyName("name")]
	public string Name { get; set; } = "";

	[JsonPropertyName("intensity")]
	public string Intensity { get; set; } = "";
}
