using System;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Core.Models
{
	public class VersionInfo
	{
		[JsonPropertyName("version")]
		public string? VersionString { get; set; }

		[JsonIgnore]
		public Version? Version => Version.TryParse(VersionString, out var v) ? v : null;

		[JsonPropertyName("message")]
		public string? Message { get; set; }

		[JsonPropertyName("time")]
		public DateTime? Time { get; set; }

		[JsonPropertyName("url")]
		public string? Url { get; set; }
	}
}
