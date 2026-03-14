using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Services.ExtarnalPublishers.P2pQuakeApi.ApiModels;

/// <summary>
/// P2P地震情報 API 地震情報メッセージ (code=551)
/// </summary>
public class P2pQuakeApiEarthquakeMessage : P2pQuakeApiBaseMessage
{
	[JsonPropertyName("issue")]
	public P2pQuakeApiEarthquakeIssue? Issue { get; set; }

	[JsonPropertyName("earthquake")]
	public P2pQuakeApiEarthquakeDetail? Earthquake { get; set; }

	[JsonPropertyName("points")]
	public P2pQuakeApiEarthquakePoint[]? Points { get; set; }
}

public class P2pQuakeApiEarthquakeIssue
{
	[JsonPropertyName("source")]
	public string? Source { get; set; }

	[JsonPropertyName("type")]
	public string? Type { get; set; }

	[JsonPropertyName("time")]
	public string? Time { get; set; }

	[JsonPropertyName("correctness")]
	public string? Correctness { get; set; }
}

public class P2pQuakeApiEarthquakeDetail
{
	[JsonPropertyName("time")]
	public string? Time { get; set; }

	[JsonPropertyName("hypocenter")]
	public P2pQuakeApiEarthquakeHypocenter? Hypocenter { get; set; }

	[JsonPropertyName("maxScale")]
	public double MaxScale { get; set; }

	[JsonPropertyName("domesticTsunami")]
	public string? DomesticTsunami { get; set; }

	[JsonPropertyName("foreignTsunami")]
	public string? ForeignTsunami { get; set; }
}

public class P2pQuakeApiEarthquakeHypocenter
{
	[JsonPropertyName("name")]
	public string? Name { get; set; }

	[JsonPropertyName("latitude")]
	public double Latitude { get; set; }

	[JsonPropertyName("longitude")]
	public double Longitude { get; set; }

	[JsonPropertyName("depth")]
	public double Depth { get; set; }

	[JsonPropertyName("magnitude")]
	public double Magnitude { get; set; }
}

public class P2pQuakeApiEarthquakePoint
{
	[JsonPropertyName("pref")]
	public string? Pref { get; set; }

	[JsonPropertyName("addr")]
	public string? Addr { get; set; }

	[JsonPropertyName("isArea")]
	public bool IsArea { get; set; }

	[JsonPropertyName("scale")]
	public double Scale { get; set; }
}
