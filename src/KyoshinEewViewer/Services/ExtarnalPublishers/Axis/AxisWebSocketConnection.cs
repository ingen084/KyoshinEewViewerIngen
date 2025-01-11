using DmdataSharp.WebSocketMessages.V2;
using KyoshinEewViewer.Services.ExtarnalPublishers.Axis.ApiModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services.ExtarnalPublishers.Axis;

public class AxisWebSocketConnection
{
	/// <summary>
	/// 接続が完了し Hello メッセージを受信した
	/// </summary>
	public event Action? Connected;

	/// <summary>
	/// エラーが発生して切断された<br/>
	/// 引数はエラー内容、リトライ不可能かどうか
	/// </summary>
	public event Action<string, bool>? Error;

	/// <summary>
	/// 切断された
	/// </summary>
	public event Action? Disconnected;

	/// <summary>
	/// メッセージを受信した
	/// </summary>
	public event Action<AxisWebSocketMessage>? MessageReceived;

	/// <summary>
	/// Ping が完了した
	/// </summary>
	public event Action<TimeSpan>? PingCompleted;

	/// <summary>
	/// WebSocketに接続中かどうか
	/// <para>Connectedイベントが発生する前のコネクション確立時にtrueになる</para>
	/// </summary>
	public bool IsConnected => WebSocket?.State == WebSocketState.Open;

	private AxisApiClient ApiClient { get; }
	private ClientWebSocket? WebSocket { get; set; }
	private CancellationTokenSource? TokenSource { get; set; }
	private Task? WebSocketConnectionTask { get; set; }

	private Timer PingTimer { get; }
	private Timer WatchDogTimer { get; }
	private Stopwatch PingStopwatch { get; } = new();

	public AxisWebSocketConnection(AxisApiClient apiClient)
	{
		ApiClient = apiClient;
		PingTimer = new Timer(async _ =>
		{
			try
			{
				if (!IsConnected || WebSocket == null)
					return;
				PingStopwatch.Restart();
				await WebSocket.SendAsync(
					Encoding.ASCII.GetBytes("hb"),
					WebSocketMessageType.Text,
					true,
					TokenSource?.Token ?? CancellationToken.None);
				Debug.WriteLine("ping send");
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"ping send error {ex}");
			}
		}, null, Timeout.Infinite, Timeout.Infinite);
		WatchDogTimer = new Timer(_ =>
		{
			if (!IsConnected)
				return;
			DisconnectAsync();
		}, null, Timeout.Infinite, Timeout.Infinite);
	}

	/// <summary>
	/// WebSocketに接続する
	/// </summary>
	/// <param name="uri">接続先のURL</param>
	/// <returns></returns>
	public async Task ConnectAsync()
	{
		if (IsConnected)
			throw new InvalidOperationException("すでにWebSocketに接続されています");

		TokenSource = new CancellationTokenSource();

		string[]? servers;
		try
		{
			servers = await ApiClient.GetServers();
		}
		catch
		{
			Error?.Invoke("接続先一覧の取得に失敗しました。", true);
			return;
		}

		WebSocket = new();
		WebSocket.Options.SetRequestHeader("Authorization", "Bearer " + ApiClient.Jwt);

		await WebSocket.ConnectAsync(new Uri(servers[new Random().Next(servers.Length)] + "/socket"), TokenSource.Token);
		WebSocketConnectionTask = ReceiverWebSocket();
	}

	private async Task ReceiverWebSocket()
	{
		var token = TokenSource?.Token ?? throw new Exception("CancellationTokenSource が初期化されていません");
		try
		{
			// 1MB
			var buffer = new byte[1024 * 1024];
			var isStarted = false;

			while (WebSocket?.State == WebSocketState.Open)
			{
				// 所得情報確保用の配列を準備
				var segment = new ArraySegment<byte>(buffer);
				// サーバからのレスポンス情報を取得
				var result = await WebSocket.ReceiveAsync(segment, token);

				// エンドポイントCloseの場合、処理を中断
				if (result.MessageType == WebSocketMessageType.Close)
				{
					Debug.WriteLine("close message によりWebSocketが切断されました。");
					await WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, token);
					OnDisconnected();
					return;
				}

				// バイナリは扱わない
				if (result.MessageType == WebSocketMessageType.Binary)
				{
					Debug.WriteLine("WebSocketでBinaryのMessageTypeが飛んできました。");
					await WebSocket.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "DO NOT READ BINARY", token);
					OnDisconnected();
					return;
				}

				// メッセージの最後まで取得
				var length = result.Count;
				while (!result.EndOfMessage)
				{
					if (length >= buffer.Length)
					{
						await WebSocket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "TOO LONG MESSAGE", token);
						OnDisconnected();
						return;
					}
					segment = new ArraySegment<byte>(buffer, length, buffer.Length - length);
					result = await WebSocket.ReceiveAsync(segment, token);

					length += result.Count;
				}

				// 各種タイマーをリセット
				PingTimer.Change(TimeSpan.FromMinutes(1), Timeout.InfiniteTimeSpan);
				WatchDogTimer.Change(TimeSpan.FromMinutes(2), Timeout.InfiniteTimeSpan);

				var messageString = Encoding.UTF8.GetString(buffer, 0, length);
				// Debug.WriteLine("resv: " + messageString);
				if (messageString == "hb")
				{
					PingStopwatch.Stop();
					PingCompleted?.Invoke(PingStopwatch.Elapsed);
					Debug.WriteLine("pong received");
					continue;
				}

				if (!isStarted)
				{
					if (messageString == "hello")
					{
						isStarted = true;
						Connected?.Invoke();
						continue;
					}

					Error?.Invoke("WebSocket接続が開始されませんでした。", true);
					await WebSocket.CloseAsync(WebSocketCloseStatus.Empty, null, token);
					OnDisconnected();
					return;
				}

				var message = JsonSerializer.Deserialize<AxisWebSocketMessage>(messageString);
				if (message == null)
				{
					Debug.WriteLine("パースに失敗しました。");
					continue;
				}
				MessageReceived?.Invoke(message);
			}
		}
		catch (TaskCanceledException)
		{
			if (IsConnected && WebSocket != null)
				await WebSocket.CloseAsync(WebSocketCloseStatus.Empty, null, token);
			OnDisconnected();
		}
		catch (Exception ex)
		{
			Debug.WriteLine("WebSocket受信スレッドで例外が発生しました\n" + ex);
			Error?.Invoke("WebSocket受信スレッドで例外が発生しました", true);
			if (IsConnected && WebSocket != null)
				await WebSocket.CloseAsync(WebSocketCloseStatus.Empty, null, token);
			OnDisconnected();
		}
	}

	/// <summary>
	/// 切断イベントを呼ぶ
	/// </summary>
	private void OnDisconnected()
	{
		PingTimer.Change(Timeout.Infinite, Timeout.Infinite);
		WatchDogTimer.Change(Timeout.Infinite, Timeout.Infinite);
		Disconnected?.Invoke();
	}

	/// <summary>
	/// WebSocketから切断する
	/// </summary>
	/// <returns></returns>
	public Task DisconnectAsync()
	{
		try
		{
			if (!IsConnected || WebSocketConnectionTask == null)
				return Task.CompletedTask;
			TokenSource?.Cancel();
			return WebSocketConnectionTask;
		}
		catch (TaskCanceledException)
		{
			return Task.CompletedTask;
		}
	}
}
