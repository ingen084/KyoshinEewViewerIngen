using KyoshinMonitorLib;
using System;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Services.ExtarnalPublishers.P2pQuakeApi.ApiModels;

/// <summary>
/// P2P地震情報 APIの基本メッセージ
/// </summary>
public class P2pQuakeApiBaseMessage
{
	[JsonPropertyName("_id")]
	public string? Id { get; set; }

	[JsonPropertyName("code")]
	public int Code { get; set; }

	[JsonPropertyName("time")]
	public string? Time { get; set; }
}

/// <summary>
/// P2P地震情報 API 緊急地震速報（警報）メッセージ (code=556)
/// </summary>
public class P2pQuakeApiEewMessage : P2pQuakeApiBaseMessage
{
	[JsonPropertyName("test")]
	public bool Test { get; set; }

	[JsonPropertyName("earthquake")]
	public P2pQuakeApiEewEarthquake? Earthquake { get; set; }

	[JsonPropertyName("issue")]
	public P2pQuakeApiEewIssue? Issue { get; set; }

	[JsonPropertyName("cancelled")]
	public bool Cancelled { get; set; }

	[JsonPropertyName("areas")]
	public P2pQuakeApiEewArea[]? Areas { get; set; }
}

public class P2pQuakeApiEewEarthquake
{
	[JsonPropertyName("originTime")]
	public string? OriginTime { get; set; }

	[JsonPropertyName("arrivalTime")]
	public string? ArrivalTime { get; set; }

	[JsonPropertyName("condition")]
	public string? Condition { get; set; }

	[JsonPropertyName("hypocenter")]
	public P2pQuakeApiEewHypocenter? Hypocenter { get; set; }
}

public class P2pQuakeApiEewHypocenter
{
	[JsonPropertyName("name")]
	public string? Name { get; set; }

	[JsonPropertyName("reduceName")]
	public string? ReduceName { get; set; }

	[JsonPropertyName("latitude")]
	public double Latitude { get; set; }

	[JsonPropertyName("longitude")]
	public double Longitude { get; set; }

	[JsonPropertyName("depth")]
	public double Depth { get; set; }

	[JsonPropertyName("magnitude")]
	public double Magnitude { get; set; }
}

public class P2pQuakeApiEewIssue
{
	[JsonPropertyName("time")]
	public string? Time { get; set; }

	[JsonPropertyName("eventId")]
	public string? EventId { get; set; }

	[JsonPropertyName("serial")]
	public string? Serial { get; set; }
}

public class P2pQuakeApiEewArea
{
	[JsonPropertyName("pref")]
	public string? Pref { get; set; }

	[JsonPropertyName("name")]
	public string? Name { get; set; }

	[JsonPropertyName("scaleFrom")]
	public double ScaleFrom { get; set; }

	[JsonPropertyName("scaleTo")]
	public double ScaleTo { get; set; }

	[JsonPropertyName("kindCode")]
	public string? KindCode { get; set; }

	[JsonPropertyName("arrivalTime")]
	public string? ArrivalTime { get; set; }
}

/// <summary>
/// P2P地震情報 APIのスケール値からJmaIntensityへの変換
/// </summary>
public static class P2pQuakeApiScaleConverter
{
	/// <summary>
	/// P2P地震情報 APIのスケール値をJmaIntensityに変換する
	/// </summary>
	/// <param name="scale">スケール値 (-1, 0, 10, 20, 30, 40, 45, 50, 55, 60, 70)</param>
	/// <returns>対応するJmaIntensity</returns>
	public static JmaIntensity ToJmaIntensity(double scale)
		=> (int)scale switch
		{
			-1 => JmaIntensity.Unknown,
			0 => JmaIntensity.Int0,
			10 => JmaIntensity.Int1,
			20 => JmaIntensity.Int2,
			30 => JmaIntensity.Int3,
			40 => JmaIntensity.Int4,
			45 => JmaIntensity.Int5Lower,
			50 => JmaIntensity.Int5Upper,
			55 => JmaIntensity.Int6Lower,
			60 => JmaIntensity.Int6Upper,
			70 => JmaIntensity.Int7,
			_ => JmaIntensity.Unknown,
		};

	/// <summary>
	/// スケール値が「～程度以上」を示すかどうか
	/// </summary>
	public static bool IsOver(double scale) => (int)scale == 99;
}
