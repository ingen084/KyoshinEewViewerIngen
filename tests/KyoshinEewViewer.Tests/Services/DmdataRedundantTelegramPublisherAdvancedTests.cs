using DmdataSharp.Interfaces;
using DmdataSharp.Redundancy;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.Services.TelegramPublishers.Dmdata;
using KyoshinEewViewer.Tests.Services.Mocks;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;

namespace KyoshinEewViewer.Tests.Services;

/// <summary>
/// DmdataRedundantTelegramPublisherの高度な機能をテストするクラス
/// CLAUDE.md原則に従い、公開APIのみを使用した適切なテスト
/// </summary>
public class DmdataRedundantTelegramPublisherAdvancedTests : IDisposable
{
	private readonly Mock<IRedundantDmdataSocketController> _mockController;
	private readonly DmdataRedundantTelegramPublisher _publisher;
	private readonly KyoshinEewViewerConfiguration _config;

	public DmdataRedundantTelegramPublisherAdvancedTests()
	{
		// Configuration setup
		_config = new KyoshinEewViewerConfiguration();
		_config.Dmdata.RefreshToken = "test-refresh-token";
		_config.Dmdata.OAuthClientId = "test-client-id";
		_config.Dmdata.UseWebSocket = true;
		_config.Dmdata.UseRedundancy = true;
		
		// Mock setup
		_mockController = new Mock<IRedundantDmdataSocketController>();
		_mockController.Setup(x => x.TotalMessagesReceived).Returns(0);
		_mockController.Setup(x => x.ActiveConnectionCount).Returns(0);
		_mockController.Setup(x => x.Status).Returns(RedundancyStatus.Disconnected);
		_mockController.Setup(x => x.ConnectedEndpoints).Returns(Array.Empty<string>());
		_mockController.Setup(x => x.IsConnected).Returns(false);
		
		// 実際の通信を行わないようにHTTPとWebSocketを差し替える
		_publisher = DmdataTestEnvironment.CreatePublisher(_config, _mockController.Object);
	}

	#region 基本的な初期化と設定のテスト

	[Fact]
	public async Task InitializeAsync_正常設定時_初期化成功する()
	{
		// Arrange
		_config.Dmdata.RefreshToken = "valid-token";
		_config.Dmdata.OAuthClientId = "valid-client-id";
		
		// Act
		var exception = await Record.ExceptionAsync(() => _publisher.InitializeAsync());
		
		// Assert - 例外が発生しないことを確認
		Assert.Null(exception);
	}

	[Fact]
	public async Task InitializeAsync_認証情報未設定時_初期化スキップされる()
	{
		// Arrange
		_config.Dmdata.RefreshToken = null;
		_config.Dmdata.OAuthClientSecret = null;
		
		// Act
		var exception = await Record.ExceptionAsync(() => _publisher.InitializeAsync());
		
		// Assert - 例外が発生しないことを確認
		Assert.Null(exception);
	}

	#endregion

	#region 公開プロパティの動作確認

	[Fact(DisplayName = "契約情報から受信できるカテゴリを判定し、WebSocketを使わない場合は緊急地震速報を含めない")]
	public async Task GetSupportedCategoriesAsync_契約に応じたカテゴリを返す()
	{
		foreach (var useWebSocket in new[] { true, false })
		{
			var config = new KyoshinEewViewerConfiguration();
			config.Dmdata.RefreshToken = "test-token";
			config.Dmdata.OAuthClientId = "test-client-id";
			config.Dmdata.UseWebSocket = useWebSocket;
			var http = new StubHttpHandler();
			http.Respond(DmdataTestEnvironment.ApiBaseUrl + "/v2/contract", DmdataTestEnvironment.ContractList("telegram.earthquake", "eew.forecast"));
			using var publisher = DmdataTestEnvironment.CreatePublisher(config, _mockController.Object, http);
			await publisher.InitializeAsync();

			var categories = await publisher.GetSupportedCategoriesAsync();

			InformationCategory[] expected = useWebSocket
				? [InformationCategory.Earthquake, InformationCategory.Tsunami, InformationCategory.EewForecast]
				: [InformationCategory.Earthquake, InformationCategory.Tsunami];
			Assert.Equal(expected.Order(), categories.Order());
		}
	}

	#endregion

	#region Start/Stop の基本動作テスト

	[Fact]
	public async Task Start_複数カテゴリ指定_正常に開始される()
	{
		// Arrange
		var categories = new[] { InformationCategory.Earthquake, InformationCategory.Tsunami };
		
		// Act
		_publisher.Start(categories);
		await Task.Delay(100); // 処理完了を待つ
		
		// Assert - 公開プロパティで状態確認
		Assert.NotEmpty(_publisher.SubscribingCategories);
		Assert.Contains(InformationCategory.Earthquake, _publisher.SubscribingCategories);
		Assert.Contains(InformationCategory.Tsunami, _publisher.SubscribingCategories);
	}

	[Fact]
	public async Task Stop_開始後の停止_正常に停止される()
	{
		// Arrange
		var categories = new[] { InformationCategory.Earthquake };
		_publisher.Start(categories);
		await Task.Delay(100); // Start処理の完了を待つ
		
		// Act
		_publisher.Stop(categories);
		await Task.Delay(100); // Stop処理の完了を待つ
		
		// Assert
		Assert.Empty(_publisher.SubscribingCategories);
	}

	#endregion




	public void Dispose()
	{
		_publisher?.Dispose();
		_mockController?.Reset();
		GC.SuppressFinalize(this);
	}
}