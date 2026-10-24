using DmdataSharp.ApiParameters.V2;
using DmdataSharp.Interfaces;
using DmdataSharp.Redundancy;
using DmdataSharp.WebSocketMessages.V2;

namespace KyoshinEewViewer.UiTests.Infrastructure.Telegram;

/// <summary>
/// dmdata の WebSocket 接続を模擬する<br/>
/// DmdataSharp と同じく、接続に失敗しても例外を投げずにイベントで通知する
/// </summary>
internal sealed class FakeDmdataSocketController(SimulatedTelegramNetwork network, IDmdataV2ApiClient apiClient) : IRedundantDmdataSocketController
{
	private const string Endpoint = "ws.fixture.invalid";

	public long TotalMessagesReceived { get; private set; }
	public long DuplicateMessagesFiltered => 0;
	public DateTime? LastMessageTime { get; private set; }
	public IDmdataV2ApiClient ApiClient => apiClient;
	public RedundantSocketOptions Options { get; } = new();
	public bool IsConnected { get; private set; }
	public int ActiveConnectionCount => IsConnected ? 1 : 0;
	public string[] ConnectedEndpoints => IsConnected ? [Endpoint] : [];
	public RedundancyStatus Status => IsConnected ? RedundancyStatus.FullyConnected : RedundancyStatus.Disconnected;

	/// <summary>
	/// 接続時に要求した受信区分
	/// </summary>
	public SocketStartRequestParameter? Parameter { get; private set; }
	public bool Disposed { get; private set; }

	public event EventHandler<DataWebSocketMessage>? DataReceived;
	public event EventHandler<RawDataReceivedEventArgs>? RawDataReceived;
	public event EventHandler<ConnectionEstablishedEventArgs>? ConnectionEstablished;
	public event EventHandler<ConnectionLostEventArgs>? ConnectionLost;
	public event EventHandler<AllConnectionsLostEventArgs>? AllConnectionsLost;
	public event EventHandler<RedundancyRestoredEventArgs>? RedundancyRestored;
	public event EventHandler<ConnectionErrorEventArgs>? ConnectionError;
	// KEVi は使用しないため発生させない
	public event EventHandler<RedundancyStatusChangedEventArgs>? RedundancyStatusChanged { add { } remove { } }

	public Task ConnectAsync(SocketStartRequestParameter param, string[]? endpoints = null)
	{
		Parameter = param;
		if (network.WebSocketError() is { } error)
		{
			ConnectionError?.Invoke(this, new ConnectionErrorEventArgs { EndpointName = Endpoint, Exception = error });
			AllConnectionsLost?.Invoke(this, new AllConnectionsLostEventArgs { LostTime = DateTime.Now, DisconnectedEndpoints = [Endpoint] });
			return Task.CompletedTask;
		}
		IsConnected = true;
		ConnectionEstablished?.Invoke(this, new ConnectionEstablishedEventArgs { EndpointName = Endpoint, ConnectedTime = DateTime.Now });
		return Task.CompletedTask;
	}

	/// <summary>
	/// 接続中のWebSocketがすべて切断されたことを模擬する (DmdataSharp は自動で再接続を試みる)
	/// </summary>
	public void Drop()
	{
		IsConnected = false;
		ConnectionLost?.Invoke(this, new ConnectionLostEventArgs { EndpointName = Endpoint, Reason = "テスト", WillReconnect = true, DisconnectedTime = DateTime.Now });
		AllConnectionsLost?.Invoke(this, new AllConnectionsLostEventArgs { LostTime = DateTime.Now, DisconnectedEndpoints = [Endpoint], WillAttemptReconnect = true });
	}

	/// <summary>
	/// DmdataSharp による自動再接続が成功したことを模擬する
	/// </summary>
	public void Reconnect()
	{
		IsConnected = true;
		ConnectionEstablished?.Invoke(this, new ConnectionEstablishedEventArgs { EndpointName = Endpoint, ConnectedTime = DateTime.Now });
		RedundancyRestored?.Invoke(this, new RedundancyRestoredEventArgs { RestoredEndpoint = Endpoint, RestoredTime = DateTime.Now, TotalActiveConnections = 1 });
	}

	/// <summary>
	/// 電文を受信したことを模擬する
	/// </summary>
	public void Receive(DataWebSocketMessage message)
	{
		if (!IsConnected)
			throw new InvalidOperationException("WebSocketが接続されていません。");
		TotalMessagesReceived++;
		LastMessageTime = DateTime.Now;
		RawDataReceived?.Invoke(this, new RawDataReceivedEventArgs { EndpointName = Endpoint, Message = message, ReceivedTime = DateTime.Now });
		DataReceived?.Invoke(this, message);
	}

	public Task DisconnectAsync()
	{
		IsConnected = false;
		return Task.CompletedTask;
	}

	public Task ReconnectEndpointAsync(string endpoint) => Task.CompletedTask;

	public void Dispose()
	{
		IsConnected = false;
		Disposed = true;
	}

	public ValueTask DisposeAsync()
	{
		Dispose();
		return ValueTask.CompletedTask;
	}
}
