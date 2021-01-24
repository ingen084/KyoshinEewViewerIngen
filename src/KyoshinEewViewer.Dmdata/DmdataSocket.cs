using KyoshinEewViewer.Dmdata.WebSocketMessages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Dmdata
{
	public class DmdataSocket : IDisposable
	{
		/// <summary>
		/// WebSocketへの接続が完了した
		/// </summary>
		public event EventHandler<StartWebSocketMessage> Connected;
		/// <summary>
		/// errorメッセージが飛んできた
		/// </summary>
		public event EventHandler<ErrorWebSocketMessage> Error;
		/// <summary>
		/// WebSocketが切断された
		/// </summary>
		public event EventHandler Disconnected;
		/// <summary>
		/// dataメッセージが飛んできた
		/// </summary>
		public event EventHandler<DataWebSocketMessage> DataReceived;
		/// <summary>
		/// WebSocketの接続数がオーバーしている
		/// </summary>
		public event EventHandler ConnectionFull;

		/// <summary>
		/// WebSocketに接続中かどうか
		/// <para>Connectedイベントが発生する前のコネクション確立時にtrueになる</para>
		/// </summary>
		public bool IsConnected => WebSocket.State == WebSocketState.Connecting;

		private ClientWebSocket WebSocket { get; } = new ClientWebSocket();
		private CancellationTokenSource TokenSource { get; set; }
		private Task WebSocketConnectionTask { get; set; }
		public DmdataApiClient ApiClient { get; }

		public DmdataSocket(DmdataApiClient apiClient)
		{
			ApiClient = apiClient;
		}

		/// <summary>
		/// WebSocketに接続する
		/// </summary>
		/// <param name="get">受信する受信区分</param>
		/// <param name="memo">管理画面に表示するメモ</param>
		/// <param name="test">訓練･試験を受け取るか</param>
		/// <returns></returns>
		public Task ConnectAsync(IEnumerable<TelegramCategory> get, string memo = null, bool test = false)
			=> ConnectAsync(string.Join(',', get.Select(g => g.ToParameterString())), memo, test);
		/// <summary>
		/// WebSocketに接続する
		/// </summary>
		/// <param name="get">受信する受信区分</param>
		/// <param name="memo">管理画面に表示するメモ</param>
		/// <param name="test">訓練･試験を受け取るか</param>
		/// <returns></returns>
		public async Task ConnectAsync(string get, string memo = null, bool test = false)
		{
			if (IsConnected)
				throw new InvalidOperationException("すでにWebSocketに接続されています");

			var resp = await ApiClient.GetSocketStartAsync(get, memo);
			TokenSource = new CancellationTokenSource();
			await ConnectAsync(new Uri(resp.Url + (test ? "&test=true" : "")));
		}
		/// <summary>
		/// WebSocketに接続する
		/// </summary>
		/// <param name="uri">接続先のURL</param>
		/// <returns></returns>
		public async Task ConnectAsync(Uri uri)
		{
			if (IsConnected)
				throw new InvalidOperationException("すでにWebSocketに接続されています");

			TokenSource = new CancellationTokenSource();
			await WebSocket.ConnectAsync(uri, TokenSource.Token);

			// TODO: 無パケット時間が続くと切断する
			WebSocketConnectionTask = Task.Run(async () =>
			{
				try
				{
					// 1MB
					var buffer = new byte[1024 * 1024];

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
							Disconnected?.Invoke(this, null);
							return;
						}

						// バイナリは扱わない
						if (result.MessageType == WebSocketMessageType.Binary)
						{
							Debug.WriteLine("WebSocketでBinaryのMessageTypeが飛んできました。");
							await WebSocket.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "DO NOT READ BINARY", TokenSource.Token);
							Disconnected?.Invoke(this, null);
							return;
						}

						// メッセージの最後まで取得
						int length = result.Count;
						while (!result.EndOfMessage)
						{
							if (length >= buffer.Length)
							{
								await WebSocket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "TOO LONG MESSAGE", TokenSource.Token);
								Disconnected?.Invoke(this, null);
								return;
							}
							segment = new ArraySegment<byte>(buffer, length, buffer.Length - length);
							result = await WebSocket.ReceiveAsync(segment, TokenSource.Token);

							length += result.Count;
						}

						var messageString = Encoding.UTF8.GetString(buffer, 0, length);
						// 接続数オーバーのチェック
						if (messageString == "The maximum number of simultaneous connections is full.")
						{
							Debug.WriteLine(messageString);
							ConnectionFull?.Invoke(this, null);
							throw new Exception(messageString);
						}

						var message = JsonSerializer.Deserialize<DmdataWebSocketMessage>(messageString);
						switch (message.Type)
						{
							case "data":
								var dataMessage = JsonSerializer.Deserialize<DataWebSocketMessage>(messageString);
								DataReceived?.Invoke(this, dataMessage);
								break;
							case "start":
								var startMessage = JsonSerializer.Deserialize<StartWebSocketMessage>(messageString);
								Connected?.Invoke(this, startMessage);
								break;
							case "error":
								var errorMessage = JsonSerializer.Deserialize<ErrorWebSocketMessage>(messageString);
								Debug.WriteLine("エラーメッセージを受信しました。");
								Error?.Invoke(this, errorMessage);
								// 切断の場合はそのまま切断する
								if (errorMessage.Action == "close")
								{
									Debug.WriteLine("切断要求のため切断扱いとします。");
									await WebSocket.CloseAsync(WebSocketCloseStatus.Empty, null, TokenSource.Token);
									Disconnected?.Invoke(this, null);
									return;
								}
								break;
							// 何もしない
							case "pong":
								break;
							case "ping":
								var pingMessage = JsonSerializer.Deserialize<PingWebSocketMessage>(messageString);
								Debug.WriteLine("pingId: " + pingMessage.PingId);
								await WebSocket.SendAsync(
									JsonSerializer.SerializeToUtf8Bytes(new PongWebSocketMessage(pingMessage)),
									WebSocketMessageType.Text,
									true,
									TokenSource.Token);
								break;
						}
					}
				}
				catch (TaskCanceledException)
				{
					await WebSocket.CloseAsync(WebSocketCloseStatus.Empty, "GOOD BYE", TokenSource.Token);
					Disconnected?.Invoke(this, null);
				}
				catch (Exception ex)
				{
					Debug.WriteLine("WebSocket受信スレッドで例外が発生しました\n" + ex);
					await WebSocket.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "CLIENT EXCEPTED", TokenSource.Token);
					Disconnected?.Invoke(this, null);
				}
			});
		}

		/// <summary>
		/// WebSocketから切断する
		/// </summary>
		/// <returns></returns>
		public Task DisconnectAsync()
		{
			if (!IsConnected)
				return Task.CompletedTask;
			TokenSource.Cancel();
			return WebSocketConnectionTask;
		}

		/// <summary>
		/// オブジェクトを破棄する
		/// </summary>
		public void Dispose()
		{
			WebSocket.Dispose();
			GC.SuppressFinalize(this);
		}
	}
}
