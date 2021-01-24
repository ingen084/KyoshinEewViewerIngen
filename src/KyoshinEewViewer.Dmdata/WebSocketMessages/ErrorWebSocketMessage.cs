using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Dmdata.WebSocketMessages
{
	public class ErrorWebSocketMessage : DmdataWebSocketMessage
	{
		public ErrorWebSocketMessage()
		{
			Type = "error";
		}

		[JsonPropertyName("error")]
		public string Error { get; set; }
		[JsonPropertyName("code")]
		public string Code { get; set; }
		[JsonPropertyName("action")]
		public string Action { get; set; }
	}
}
