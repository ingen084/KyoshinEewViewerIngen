using System;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Dmdata.WebSocketMessages
{
	public class StartWebSocketMessage : DmdataWebSocketMessage
	{
		public StartWebSocketMessage()
		{
			Type = "start";
		}
		[JsonPropertyName("classification")]
		public string[] Classification { get; set; }
		[JsonPropertyName("time")]
		public DateTime Time { get; set; }
	}

}
