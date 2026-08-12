using System.Text.Json;
using System.Text.Json.Serialization;

#pragma warning disable CS8618 // JSON DTOs are populated by deserialization.

namespace KyoshinEewViewer.Services.ExternalPublishers.Axis.ApiModels;

public class AxisWebSocketMessage
{
	[JsonPropertyName("channel")]
	public string Channel { get; set; }

	[JsonPropertyName("message")]
	public JsonElement Message { get; set; }
}
