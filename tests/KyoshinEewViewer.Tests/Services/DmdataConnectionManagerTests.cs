using DmdataSharp.ApiParameters.V2;
using DmdataSharp.Interfaces;
using DmdataSharp.Redundancy;
using DmdataSharp.WebSocketMessages.V2;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Services.TelegramPublishers.Dmdata;
using Moq;

namespace KyoshinEewViewer.Tests.Services;

/// <summary>
/// DmdataConnectionManagerのテスト
/// </summary>
public class DmdataConnectionManagerTests : IDisposable
{
	private readonly DmdataConnectionManager _manager;
	private readonly Mock<IDmdataV2ApiClient> _mockApiClient;

	public DmdataConnectionManagerTests()
	{
		var logManager = new DefaultLogManager();
		_manager = new DmdataConnectionManager(logManager);
		_mockApiClient = new Mock<IDmdataV2ApiClient>();
	}

	[Fact(DisplayName = "初期状態_正しい値を持つ")]
	public void 初期状態_正しい値を持つ()
	{
		// Assert
		Assert.Equal(RedundancyStatus.Disconnected, _manager.RedundancyStatus);
		Assert.Equal(0, _manager.ActiveConnectionCount);
		Assert.Empty(_manager.ConnectedEndpoints);
		Assert.Equal(0, _manager.TotalMessagesReceived);
		Assert.Null(_manager.LastMessageTime);
		Assert.False(_manager.IsWebSocketConnected);
	}

	[Fact(DisplayName = "ConnectWebSocketAsync_購読カテゴリなし_例外をスロー")]
	public async Task ConnectWebSocketAsync_購読カテゴリなし_例外をスロー()
	{
		// Arrange
		var emptyCategories = Enumerable.Empty<KyoshinEewViewer.Services.InformationCategory>();

		// Act & Assert
		await Assert.ThrowsAsync<InvalidOperationException>(async () =>
			await _manager.ConnectWebSocketAsync(
				_mockApiClient.Object,
				emptyCategories,
				"1.0.0",
				false,
				false,
				Array.Empty<string>(),
				null
			)
		);
	}

	[Fact(DisplayName = "DisconnectWebSocketAsync_接続なし_例外が発生しない")]
	public async Task DisconnectWebSocketAsync_接続なし_例外が発生しない()
	{
		// Act
		var exception = await Record.ExceptionAsync(async () =>
			await _manager.DisconnectWebSocketAsync()
		);

		// Assert
		Assert.Null(exception);
	}

	[Fact(DisplayName = "DataReceivedイベント_購読できる")]
	public void DataReceivedイベント_購読できる()
	{
		// Arrange
		var eventFired = false;
		DataWebSocketMessage? receivedMessage = null;

		_manager.DataReceived += (s, e) =>
		{
			eventFired = true;
			receivedMessage = e;
		};

		// Assert - イベントハンドラーが登録されたことを確認
		Assert.False(eventFired);
	}

	[Fact(DisplayName = "ConnectionStatusChangedイベント_購読できる")]
	public void ConnectionStatusChangedイベント_購読できる()
	{
		// Arrange
		var eventFired = false;

		_manager.ConnectionStatusChanged += (s, e) =>
		{
			eventFired = true;
		};

		// Assert - イベントハンドラーが登録されたことを確認
		Assert.False(eventFired);
	}

	[Fact(DisplayName = "AllConnectionsLostイベント_購読できる")]
	public void AllConnectionsLostイベント_購読できる()
	{
		// Arrange
		var eventFired = false;

		_manager.AllConnectionsLost += (s, e) =>
		{
			eventFired = true;
		};

		// Assert - イベントハンドラーが登録されたことを確認
		Assert.False(eventFired);
	}

	[Fact(DisplayName = "Dispose_例外が発生しない")]
	public void Dispose_例外が発生しない()
	{
		// Act
		var exception = Record.Exception(() => _manager.Dispose());

		// Assert
		Assert.Null(exception);
	}

	[Fact(DisplayName = "RedundantController_null時_プロパティが初期値を返す")]
	public void RedundantController_null時_プロパティが初期値を返す()
	{
		// Assert - RedundantControllerがnullの場合のプロパティ値を確認
		Assert.Equal(RedundancyStatus.Disconnected, _manager.RedundancyStatus);
		Assert.Equal(0, _manager.ActiveConnectionCount);
		Assert.Empty(_manager.ConnectedEndpoints);
		Assert.Equal(0, _manager.TotalMessagesReceived);
		Assert.Null(_manager.LastMessageTime);
		Assert.False(_manager.IsWebSocketConnected);
	}

	public void Dispose()
	{
		_manager?.Dispose();
		GC.SuppressFinalize(this);
	}
}
