using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebSocket4Net;

namespace LightningMapTest
{
	public class LightningMapConnection
	{
		public event Action<Lighitning> Arrived;

		private WebSocket WebSocket;

		private string[] WebSocketServers = {
			"ws5.blitzortung.org",
			"ws4.blitzortung.org",
			"ws3.blitzortung.org",
			"ws1.blitzortung.org",
		};

		// WebSocketに接続
		public void Connect()
		{
			var random = new Random();
			var server = WebSocketServers[random.Next(0, WebSocketServers.Length - 1)];

			//クライアント側のWebSocketを定義
			WebSocket = new WebSocket($"wss://{server}:3000/");

			WebSocket.Opened += (s, e) =>
			{
				WebSocket.Send("{\"time\":0}");
				Debug.WriteLine("Opened");
			};
			WebSocket.MessageReceived += (s, e) =>
			{
				Arrived?.Invoke(JsonConvert.DeserializeObject<Lighitning>(e.Message));
			};
			WebSocket.Closed += (s, e) => Debug.WriteLine("Closed");
			WebSocket.Error += (s, e) => Debug.WriteLine("Error");

			WebSocket.AutoSendPingInterval = 30;
			WebSocket.EnableAutoSendPing = true;

			WebSocket.Open();
		}

		public void Disconnect()
		{
			WebSocket.Dispose();
		}
	}

	public class Lighitning
	{
		public long time { get; set; }
		public float lat { get; set; }
		public float lon { get; set; }
		public int alt { get; set; }
		public int pol { get; set; }
		public int mds { get; set; }
		public int mcg { get; set; }
		public int status { get; set; }
		public int region { get; set; }
		public Sig[] sig { get; set; }
		public float delay { get; set; }

		public class Sig
		{
			public int sta { get; set; }
			public int time { get; set; }
			public float lat { get; set; }
			public float lon { get; set; }
			public int alt { get; set; }
			public int status { get; set; }
		}
	}
}
