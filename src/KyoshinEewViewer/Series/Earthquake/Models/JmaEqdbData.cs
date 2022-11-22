using KyoshinMonitorLib;
using System;
using System.Globalization;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Series.Earthquake.Models;

public class JmaEqdbData
{
	[JsonPropertyName("res")]
	public Response? Res { get; set; }
	public class Response
	{
		[JsonPropertyName("hyp")]
		public HypoCenter[]? HypoCenters { get; set; }
		[JsonPropertyName("int")]
		public IntensityStation[]? IntensityStations { get; set; }
	}

	public class HypoCenter
	{
		/// <summary>
		/// イベントID
		/// </summary>
		[JsonPropertyName("id")]
		public string? Id { get; set; }
		/// <summary>
		/// 発生時刻
		/// </summary>
		[JsonPropertyName("ot")]
		public string? OccurrenceTime { get; set; }
		/// <summary>
		/// 震央名
		/// </summary>
		[JsonPropertyName("name")]
		public string? Name { get; set; }
		[JsonPropertyName("lat")]
		public string? Latitude { get; set; }
		[JsonPropertyName("lon")]
		public string? Longitude { get; set; }
		[JsonIgnore]
		public Location? Location
		{
			get {
				if (float.TryParse(Latitude, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var lat) &&
					float.TryParse(Longitude, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var lon))
					return new Location(lat, lon);
				return null;
			}
		}
		/// <summary>
		/// X km
		/// </summary>
		[JsonPropertyName("dep")]
		public string? Depth { get; set; }
		public int? DepthKm
		{
			get {
				if (Depth is null || !Depth.EndsWith(" km") || !int.TryParse(Depth.Replace(" km", ""), out var depth))
					return null;
				return depth;
			}
		}
		/// <summary>
		/// X.X
		/// </summary>
		[JsonPropertyName("mag")]
		public string? Magnitude { get; set; }
		/// <summary>
		/// 震度X(全角)
		/// </summary>
		[JsonPropertyName("maxI")]
		public string? RawMaxIntensity { get; set; }
		public JmaIntensity MaxIntensity => RawMaxIntensity?.ToJmaIntensity() ?? JmaIntensity.Int0;
	}

	public class IntensityStation
	{
		[JsonPropertyName("code")]
		public string? Code { get; set; }
		[JsonPropertyName("name")]
		public string? Name { get; set; }
		[JsonPropertyName("lat")]
		public string? Latitude { get; set; }
		[JsonPropertyName("lon")]
		public string? Longitude { get; set; }
		[JsonIgnore]
		public Location? Location
		{
			get {
				if (float.TryParse(Latitude, out var lat) && float.TryParse(Longitude, out var lon))
					return new Location(lat, lon);
				return null;
			}
		}
		[JsonPropertyName("int")]
		public string? RawIntensity { get; set; }
		public JmaIntensity Intensity => RawIntensity?.ToJmaIntensity() ?? JmaIntensity.Int0;
	}
}
