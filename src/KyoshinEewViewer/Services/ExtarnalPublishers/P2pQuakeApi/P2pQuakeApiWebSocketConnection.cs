using KyoshinEewViewer.Core;
using KyoshinEewViewer.Services.ExtarnalPublishers.P2pQuakeApi.ApiModels;
using Splat;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services.ExtarnalPublishers.P2pQuakeApi;

public class P2pQuakeApiWebSocketConnection
{
	private const string WebSocketUrl = "wss://api.p2pquake.net/v2/ws";

	private ILogger Logger { get; }

	public P2pQuakeApiWebSocketConnection(ILogManager logManager)
	{
		Logger = logManager.GetLogger<P2pQuakeApiWebSocketConnection>();
	}

	/// <summary>
	/// 接続が完了した
	/// </summary>
	public event Action? Connected;

	/// <summary>
	/// エラーが発生して切断された
	/// </summary>
	public event Action<string>? Error;

	/// <summary>
	/// 切断された
	/// </summary>
	public event Action? Disconnected;

	/// <summary>
	/// メッセージを受信した
	/// </summary>
	public event Action<P2pQuakeApiBaseMessage>? MessageReceived;

	/// <summary>
	/// WebSocketに接続中かどうか
	/// </summary>
	public bool IsConnected => WebSocket?.State == WebSocketState.Open;

	/// <summary>
	/// 受信済みメッセージIDの最大保持数
	/// </summary>
	private const int MaxReceivedIdCount = 250;

	private ClientWebSocket? WebSocket { get; set; }
	private CancellationTokenSource? TokenSource { get; set; }
	private Task? WebSocketConnectionTask { get; set; }
	private HashSet<string> ReceivedIds { get; } = [];
	private Queue<string> ReceivedIdOrder { get; } = new();
#if DEBUG
	private StreamWriter? DebugLogWriter { get; set; }
#endif

	/// <summary>
	/// WebSocketに接続する
	/// </summary>
	public async Task ConnectAsync()
	{
		if (IsConnected)
			throw new InvalidOperationException("すでにWebSocketに接続されています");

		// 前回の接続リソースを破棄
		TokenSource?.Dispose();
		WebSocket?.Dispose();

		TokenSource = new CancellationTokenSource();

		WebSocket = new();
		WebSocket.Options.SetRequestHeader("User-Agent", $"KEVi_{Core.Utils.Version};twitter@ingen084");

#if DEBUG
		// デバッグ用受信ログファイルを開く
		try
		{
			var logDir = Path.Combine(PlatformDirectories.Logs, "P2pQuakeApi");
			PlatformDirectories.EnsureDirectoryExists(logDir);
			var logPath = Path.Combine(logDir, $"received_{DateTime.Now:yyyyMMdd_HHmmss}.log");
			DebugLogWriter = new StreamWriter(logPath, append: true, Encoding.UTF8) { AutoFlush = true };
			Logger.LogInfo($"P2P地震情報 受信ログファイルを作成しました: {logPath}");
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "P2P地震情報 受信ログファイルの作成に失敗しました");
		}
#endif

		Logger.LogInfo("P2P地震情報 WebSocketへ接続を開始します");
		await WebSocket.ConnectAsync(new Uri(WebSocketUrl), TokenSource.Token);
		Logger.LogInfo("P2P地震情報 WebSocketへの接続が完了しました");
		Connected?.Invoke();
		WebSocketConnectionTask = ReceiverWebSocket();
	}

	private async Task ReceiverWebSocket()
	{
		var token = TokenSource?.Token ?? throw new Exception("CancellationTokenSource が初期化されていません");
		try
		{
			// 1MB
			var buffer = new byte[1024 * 1024];

			while (WebSocket?.State == WebSocketState.Open)
			{
				var segment = new ArraySegment<byte>(buffer);
				var result = await WebSocket.ReceiveAsync(segment, token);

				if (result.MessageType == WebSocketMessageType.Close)
				{
					Logger.LogInfo("P2P地震情報 WebSocketがサーバーからのCloseメッセージにより切断されました");
					await TryCloseAsync(WebSocketCloseStatus.NormalClosure, null, token);
					return;
				}

				if (result.MessageType == WebSocketMessageType.Binary)
				{
					Logger.LogWarning("P2P地震情報 WebSocketでBinaryメッセージを受信したため切断します");
					await TryCloseAsync(WebSocketCloseStatus.InvalidMessageType, "DO NOT READ BINARY", token);
					return;
				}

				var length = result.Count;
				while (!result.EndOfMessage)
				{
					if (length >= buffer.Length)
					{
						await TryCloseAsync(WebSocketCloseStatus.MessageTooBig, "TOO LONG MESSAGE", token);
						return;
					}
					segment = new ArraySegment<byte>(buffer, length, buffer.Length - length);
					result = await WebSocket.ReceiveAsync(segment, token);
					length += result.Count;
				}

				var messageString = Encoding.UTF8.GetString(buffer, 0, length);
				Logger.LogDebug(messageString);
#if DEBUG
				DebugLogWriter?.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {messageString}");
#endif
				var message = ParseMessage(messageString);
				if (message == null)
					continue;

				if (message.Id != null)
				{
					if (!ReceivedIds.Add(message.Id))
					{
						Logger.LogDebug($"P2P地震情報 重複メッセージをスキップしました: {message.Id}");
						continue;
					}
					ReceivedIdOrder.Enqueue(message.Id);
					while (ReceivedIds.Count > MaxReceivedIdCount)
						ReceivedIds.Remove(ReceivedIdOrder.Dequeue());
				}

				MessageReceived?.Invoke(message);
			}

			// whileループの条件不成立で抜けた場合（Stateが静かに変化した場合）
			Logger.LogWarning($"P2P地震情報 WebSocketの状態が変化したためループを終了しました: {WebSocket?.State}");
		}
		catch (TaskCanceledException)
		{
			Logger.LogInfo("P2P地震情報 WebSocketがキャンセルにより切断されます");
			await TryCloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "P2P地震情報 WebSocket受信スレッドで例外が発生しました");
			Error?.Invoke("WebSocket受信スレッドで例外が発生しました");
			await TryCloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
		}
		finally
		{
			OnDisconnected();
		}
	}

	/// <summary>
	/// WebSocketのCloseを安全に試行する
	/// </summary>
	private async Task TryCloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken token)
	{
		if (WebSocket is not { State: WebSocketState.Open or WebSocketState.CloseReceived })
			return;
		try
		{
			await WebSocket.CloseAsync(closeStatus, statusDescription, token);
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "P2P地震情報 WebSocketのClose処理中にエラーが発生しました");
		}
	}

	/// <summary>
	/// JSON文字列をcodeに応じた型にパースする
	/// </summary>
	private static P2pQuakeApiBaseMessage? ParseMessage(string json)
	{
		var baseMessage = JsonSerializer.Deserialize<P2pQuakeApiBaseMessage>(json);
		if (baseMessage == null)
			return null;

		return baseMessage.Code switch
		{
			551 => JsonSerializer.Deserialize<P2pQuakeApiEarthquakeMessage>(json),
			556 => JsonSerializer.Deserialize<P2pQuakeApiEewMessage>(json),
			_ => baseMessage,
		};
	}

	private void OnDisconnected()
	{
#if DEBUG
		DebugLogWriter?.Dispose();
		DebugLogWriter = null;
#endif
		Disconnected?.Invoke();
	}

	/// <summary>
	/// WebSocketから切断する
	/// </summary>
	public async Task DisconnectAsync()
	{
		if (WebSocketConnectionTask == null)
			return;

		Logger.LogInfo("P2P地震情報 WebSocketの切断を開始します");
		TokenSource?.Cancel();

		try
		{
			await WebSocketConnectionTask;
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "P2P地震情報 WebSocket切断タスクの待機中にエラーが発生しました");
		}
	}
}
