using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Series.Radar.Models;

public class GeoJson
{
	[JsonPropertyName("type")]
	public GeoJsonFeatureType? Type { get; set; }
	[JsonPropertyName("properties")]
	public Dictionary<string, string>? Properties { get; set; }
	[JsonPropertyName("features")]
	public GeoJson[]? Features { get; set; }
	[JsonPropertyName("geometry")]
	public GeoJson? Geometry { get; set; }
	[JsonPropertyName("coordinates")]
	public float[][][]? Coordinates { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter<GeoJsonFeatureType>))]
public enum GeoJsonFeatureType
{
	FeatureCollection,
	Feature,
	Polygon,
}
