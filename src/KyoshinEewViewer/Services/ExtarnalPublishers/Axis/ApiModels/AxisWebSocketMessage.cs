using System.Text.Json;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Services.ExtarnalPublishers.Axis.ApiModels;

public class AxisWebSocketMessage
{
	[JsonPropertyName("channel")]
	public string Channel { get; set; }

	[JsonPropertyName("message")]
	public JsonElement Message { get; set; }
}
