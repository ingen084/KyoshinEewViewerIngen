using System;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.Lightning;

public class LightningMapConnection
{
	public event Action<Lighitning?>? Arrived;
	public event Action? Disconnected;

	private ClientWebSocket WebSocket { get; } = new();
	private Task? WebSocketConnectionTask { get; set; }
	private CancellationTokenSource? TokenSource { get; set; }
	private Timer? Timer { get; set; }
	public bool IsConnected => WebSocket.State == WebSocketState.Open;

	private readonly string[] _webSocketServers = [
			"ws6.blitzortung.org",
			"ws7.blitzortung.org",
			"ws8.blitzortung.org",
		];

	// WebSocketに接続
	public async Task ConnectAsync()
	{
		Debug.WriteLine("connect");
		var random = new Random();
		var server = _webSocketServers[random.Next(0, _webSocketServers.Length - 1)];

		TokenSource = new CancellationTokenSource();
		//クライアント側のWebSocketを定義
		await WebSocket.ConnectAsync(new Uri($"wss://{server}/"), TokenSource.Token);
		Timer = new Timer(async s =>
		{
			await WebSocket.SendAsync(Encoding.UTF8.GetBytes("{\"wsServer\":\"" + server + "\"}"),
									WebSocketMessageType.Text,
									true,
									TokenSource.Token);
			//Debug.WriteLine("ping sent: " + "{\"wsServer\":\"" + server + "\"}");
		}, null, Timeout.Infinite, Timeout.Infinite);
		WebSocketConnectionTask = new Task(async () =>
		{
			try
			{
				// 1MB
				var buffer = new byte[1024 * 1024];

				await WebSocket.SendAsync(Encoding.UTF8.GetBytes("{\"time\":0}"),
										WebSocketMessageType.Text,
										true,
										TokenSource.Token);

				while (WebSocket.State == WebSocketState.Open)
				{
					// 所得情報確保用の配列を準備
					var segment = new ArraySegment<byte>(buffer);
					// サーバからのレスポンス情報を取得
					var result = await WebSocket.ReceiveAsync(segment, TokenSource.Token);

					// エンドポイントCloseの場合、処理を中断
					if (result.MessageType == WebSocketMessageType.Close)
					{
						Debug.WriteLine("WebSocketが切断されました。");
						await WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "OK", TokenSource.Token);
						OnDisconnected();
						return;
					}

					// バイナリは扱わない
					if (result.MessageType == WebSocketMessageType.Binary)
					{
						Debug.WriteLine("WebSocketでBinaryのMessageTypeが飛んできました。");
						await WebSocket.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "DO NOT READ BINARY", TokenSource.Token);
						Debug.WriteLine("DO NOT READ BINARY");
						Disconnected?.Invoke();
						return;
					}

					// メッセージの最後まで取得
					var length = result.Count;
					while (!result.EndOfMessage)
					{
						if (length >= buffer.Length)
						{
							await WebSocket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "TOO LONG MESSAGE", TokenSource.Token);
							Debug.WriteLine("TOO LONG MESSAGE");
							Disconnected?.Invoke();
							return;
						}
						segment = new ArraySegment<byte>(buffer, length, buffer.Length - length);
						result = await WebSocket.ReceiveAsync(segment, TokenSource.Token);

						length += result.Count;
					}

					var message = Encoding.UTF8.GetString(buffer, 0, length);
					//Debug.WriteLine(message);
					Arrived?.Invoke(JsonSerializer.Deserialize<Lighitning>(message));
				}
			}
			catch (TaskCanceledException)
			{
				if (IsConnected)
					await WebSocket.CloseAsync(WebSocketCloseStatus.Empty, null, TokenSource.Token);
				OnDisconnected();
			}
			catch (Exception ex)
			{
				Debug.WriteLine("WebSocket受信スレッドで例外が発生しました\n" + ex);
				await WebSocket.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "CLIENT EXCEPTED", TokenSource.Token);
				OnDisconnected();
			}
		}, TokenSource.Token, TaskCreationOptions.LongRunning);
		WebSocketConnectionTask.Start();

		Timer.Change(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
	}

	private void OnDisconnected()
	{
		Debug.WriteLine("Closed");
		//IsDisposed = true;
		Timer?.Change(Timeout.Infinite, Timeout.Infinite);
		//Disconnected?.Invoke(this, null);
	}
}

public class Lighitning
{
	[JsonPropertyName("time")]
	public long Time { get; set; }
	[JsonPropertyName("lat")]
	public float Lat { get; set; }
	[JsonPropertyName("lon")]
	public float Lon { get; set; }
	[JsonPropertyName("alt")]
	public int Alt { get; set; }
	[JsonPropertyName("pol")]
	public int Pol { get; set; }
	[JsonPropertyName("mds")]
	public int Mds { get; set; }
	[JsonPropertyName("mcg")]
	public int Mcg { get; set; }
	[JsonPropertyName("status")]
	public int Status { get; set; }
	[JsonPropertyName("region")]
	public int Region { get; set; }
	[JsonPropertyName("sig")]
	public SigData[]? Sig { get; set; }
	[JsonPropertyName("delay")]
	public float Delay { get; set; }

	public class SigData
	{
		[JsonPropertyName("sta")]
		public int Sta { get; set; }
		[JsonPropertyName("time")]
		public int Time { get; set; }
		[JsonPropertyName("lat")]
		public float Lat { get; set; }
		[JsonPropertyName("lon")]
		public float Lon { get; set; }
		[JsonPropertyName("alt")]
		public int Alt { get; set; }
		[JsonPropertyName("status")]
		public int Status { get; set; }
	}
}
