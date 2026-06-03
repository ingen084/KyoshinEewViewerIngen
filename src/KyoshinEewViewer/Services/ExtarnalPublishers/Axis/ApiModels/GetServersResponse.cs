using System.Text.Json.Serialization;

#pragma warning disable CS8618 // JSON DTOs are populated by deserialization.

namespace KyoshinEewViewer.Services.ExtarnalPublishers.Axis.ApiModels;

public class GetServersResponse
{
	[JsonPropertyName("status")]
	public string Status { get; set; }

	[JsonPropertyName("servers")]
	public string[]? Servers { get; set; }
}
