using DmdataSharp.Interfaces;
using DmdataSharp.Redundancy;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.Services.TelegramPublishers.Dmdata;
using Moq;

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
		
		var logManager = new DefaultLogManager();
		var cacheService = new InformationCacheService(logManager);
		
		_publisher = new DmdataRedundantTelegramPublisher(logManager, _config, cacheService);
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

	[Fact]
	public async Task GetSupportedCategoriesAsync_認証情報設定時_正常動作する()
	{
		// Arrange
		_config.Dmdata.RefreshToken = "test-token";
		_config.Dmdata.OAuthClientId = "test-client-id";
		await _publisher.InitializeAsync();
		
		// Act
		var categories = await _publisher.GetSupportedCategoriesAsync();
		
		// Assert - モック環境では空配列が返される
		Assert.NotNull(categories);
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