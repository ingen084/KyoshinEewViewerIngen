using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Services.ExtarnalPublishers.Axis.ApiModels;

public class GetServersResponse
{
	[JsonPropertyName("status")]
	public string Status { get; set; }

	[JsonPropertyName("servers")]
	public string[]? Servers { get; set; }
}
