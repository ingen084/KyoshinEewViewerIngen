using System;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Series.Radar.Models
{

	public class JmaRadarTime
	{
		[JsonPropertyName("basetime")]
		public string? BaseTime { get; set; }
		[JsonIgnore]
		public DateTime? BaseDateTime => DateTime.TryParseExact(BaseTime, "yyyyMMddHHmmss", null, System.Globalization.DateTimeStyles.None, out var time)
					? time
					: null;
		[JsonPropertyName("validtime")]
		public string? ValidTime { get; set; }
		[JsonIgnore]
		public DateTime? ValidDateTime => DateTime.TryParseExact(ValidTime, "yyyyMMddHHmmss", null, System.Globalization.DateTimeStyles.None, out var time)
					? time
					: null;
		[JsonPropertyName("elements")]
		public string[]? Elements { get; set; }
	}
}
