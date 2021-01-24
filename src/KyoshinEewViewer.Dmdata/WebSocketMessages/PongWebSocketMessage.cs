using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Dmdata.WebSocketMessages
{
	public class PongWebSocketMessage : DmdataWebSocketMessage
	{
		public PongWebSocketMessage()
		{
			Type = "pong";
		}
		public PongWebSocketMessage(PingWebSocketMessage ping)
		{
			Type = "pong";
			PingId = ping.PingId;
		}

		/// <summary>
		/// PONGのID
		/// </summary>
		[JsonPropertyName("pingId")]
		public string PingId { get; set; }
	}
}
