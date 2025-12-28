using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ShakeDetectWebSocketServer;

/// <summary>
/// WebSocket接続管理とイベント配信を行うハンドラー
/// </summary>
public class WebSocketHandler
{
	private static readonly ActivitySource ActivitySource = new("ShakeDetectWebSocketServer");
	private static readonly Meter Meter = new("ShakeDetectWebSocketServer");

	// メトリクス
	private static readonly UpDownCounter<int> ConnectionsCounter = Meter.CreateUpDownCounter<int>(
		"websocket.connections",
		"connections",
		"Current number of WebSocket connections");

	private static readonly Counter<long> MessagesSentCounter = Meter.CreateCounter<long>(
		"websocket.messages_sent",
		"messages",
		"Total number of messages sent to WebSocket clients");

	private readonly ConcurrentDictionary<string, WebSocket> _connections = new();
	private readonly ILogger<WebSocketHandler> _logger;

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
	};

	public WebSocketHandler(ILogger<WebSocketHandler> logger)
	{
		_logger = logger;
	}

	/// <summary>
	/// 現在の接続数
	/// </summary>
	public int ConnectionCount => _connections.Count;

	/// <summary>
	/// WebSocket接続を処理する
	/// </summary>
	public async Task HandleConnectionAsync(WebSocket webSocket, CancellationToken cancellationToken)
	{
		var connectionId = Guid.NewGuid().ToString();

		using var activity = ActivitySource.StartActivity("websocket.connection");
		activity?.SetTag("connection.id", connectionId);

		_connections.TryAdd(connectionId, webSocket);
		ConnectionsCounter.Add(1);
		_logger.LogInformation("WebSocket接続を確立しました: {ConnectionId}", connectionId);

		try
		{
			var buffer = new byte[1024 * 4];
			while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
			{
				var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

				if (result.MessageType == WebSocketMessageType.Close)
				{
					await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
					break;
				}
			}
		}
		catch (WebSocketException ex)
		{
			_logger.LogWarning(ex, "WebSocket接続でエラーが発生しました: {ConnectionId}", connectionId);
		}
		catch (OperationCanceledException)
		{
			_logger.LogInformation("WebSocket接続がキャンセルされました: {ConnectionId}", connectionId);
		}
		finally
		{
			_connections.TryRemove(connectionId, out _);
			ConnectionsCounter.Add(-1);
			_logger.LogInformation("WebSocket接続を終了しました: {ConnectionId}", connectionId);
		}
	}

	/// <summary>
	/// すべての接続にペイロードを送信する
	/// </summary>
	public async Task BroadcastAsync(WebSocketPayload payload, CancellationToken cancellationToken = default)
	{
		if (_connections.IsEmpty)
			return;

		using var activity = ActivitySource.StartActivity("websocket.broadcast");
		activity?.SetTag("payload.type", payload.GetType().Name);
		activity?.SetTag("connections.count", _connections.Count);

		var json = JsonSerializer.Serialize(payload, JsonOptions);
		var bytes = Encoding.UTF8.GetBytes(json);
		var segment = new ArraySegment<byte>(bytes);

		var tasks = new List<Task>();
		var disconnectedIds = new List<string>();

		foreach (var (connectionId, socket) in _connections)
		{
			if (socket.State != WebSocketState.Open)
			{
				disconnectedIds.Add(connectionId);
				continue;
			}

			tasks.Add(SendToSocketAsync(connectionId, socket, segment, cancellationToken));
		}

		await Task.WhenAll(tasks);

		// 切断された接続を削除
		foreach (var id in disconnectedIds)
		{
			if (_connections.TryRemove(id, out _))
			{
				ConnectionsCounter.Add(-1);
				_logger.LogDebug("切断された接続を削除しました: {ConnectionId}", id);
			}
		}

		MessagesSentCounter.Add(_connections.Count);
		activity?.SetTag("messages.sent", _connections.Count);
	}

	private async Task SendToSocketAsync(string connectionId, WebSocket socket, ArraySegment<byte> data, CancellationToken cancellationToken)
	{
		try
		{
			await socket.SendAsync(data, WebSocketMessageType.Text, true, cancellationToken);
		}
		catch (WebSocketException ex)
		{
			_logger.LogWarning(ex, "WebSocket送信でエラーが発生しました: {ConnectionId}", connectionId);
			if (_connections.TryRemove(connectionId, out _))
			{
				ConnectionsCounter.Add(-1);
			}
		}
		catch (OperationCanceledException)
		{
			// キャンセル時は無視
		}
	}

	/// <summary>
	/// すべての接続を閉じる
	/// </summary>
	public async Task CloseAllAsync(CancellationToken cancellationToken = default)
	{
		var tasks = new List<Task>();

		foreach (var (connectionId, socket) in _connections)
		{
			if (socket.State == WebSocketState.Open)
			{
				tasks.Add(CloseSocketAsync(connectionId, socket, cancellationToken));
			}
		}

		await Task.WhenAll(tasks);
		_connections.Clear();
	}

	private async Task CloseSocketAsync(string connectionId, WebSocket socket, CancellationToken cancellationToken)
	{
		try
		{
			await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server shutting down", cancellationToken);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "WebSocket終了でエラーが発生しました: {ConnectionId}", connectionId);
		}
	}
}
