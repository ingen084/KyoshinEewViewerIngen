using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Dmdata.WebSocketMessages
{
	public class PingWebSocketMessage : DmdataWebSocketMessage
	{
		public PingWebSocketMessage()
		{
			Type = "ping";
		}

		/// <summary>
		/// PINGのID
		/// </summary>
		[JsonPropertyName("pingId")]
		public string PingId { get; set; }
	}
}
