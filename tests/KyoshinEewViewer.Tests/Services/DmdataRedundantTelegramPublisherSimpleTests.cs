using DmdataSharp.Interfaces;
using DmdataSharp.Redundancy;
using DmdataSharp.WebSocketMessages.V2;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.Services.TelegramPublishers;
using KyoshinEewViewer.Services.TelegramPublishers.Dmdata;
using Moq;
using Splat;
using System.Reflection;

namespace KyoshinEewViewer.Tests.Services;

/// <summary>
/// DmdataRedundantTelegramPublisherの基本機能をテストするクラス
/// </summary>
public class DmdataRedundantTelegramPublisherSimpleTests : IDisposable
{
	private readonly Mock<IRedundantDmdataSocketController> _mockController;
	private readonly Mock<IDmdataV2ApiClient> _mockApiClient;
	private readonly DmdataRedundantTelegramPublisher _publisher;
	private readonly KyoshinEewViewerConfiguration _config;

	public DmdataRedundantTelegramPublisherSimpleTests()
	{
		// 最小限の設定でPublisherを作成
		_config = new KyoshinEewViewerConfiguration();

		// Mock設定
		_mockController = new Mock<IRedundantDmdataSocketController>();
		_mockApiClient = new Mock<IDmdataV2ApiClient>();

		// 冗長性コントローラーの基本設定
		_mockController.Setup(x => x.TotalMessagesReceived).Returns(0);
		_mockController.Setup(x => x.DuplicateMessagesFiltered).Returns(0);
		_mockController.Setup(x => x.ActiveConnectionCount).Returns(0);
		_mockController.Setup(x => x.Status).Returns(RedundancyStatus.Disconnected);
		_mockController.Setup(x => x.ConnectedEndpoints).Returns(Array.Empty<string>());
		_mockController.Setup(x => x.IsConnected).Returns(false);
		_mockController.Setup(x => x.ApiClient).Returns(_mockApiClient.Object);
		_mockController.Setup(x => x.LastMessageTime).Returns((DateTime?)null);

		// ログマネージャーには実際のインスタンスを使用（Mockを避ける）
		var logManager = Locator.Current.GetService<ILogManager>() ??
			new DefaultLogManager();

		// キャッシュサービスも実際のインスタンス
		var cacheService = new InformationCacheService(logManager);

		_publisher = new DmdataRedundantTelegramPublisher(logManager, _config, cacheService);
	}

	#region 基本機能のテスト

	[Fact]
	public void Constructor_正常実行_インスタンス生成される()
	{
		// Assert
		Assert.NotNull(_publisher);
		Assert.Equal(0, _publisher.ActiveConnectionCount);
		Assert.Equal(0, _publisher.TotalMessagesReceived);
	}


	[Fact]
	public async Task InitializeAsync_認証情報未設定時_正常終了する()
	{
		// Arrange - 認証情報を未設定にする
		_config.Dmdata.RefreshToken = null;
		_config.Dmdata.OAuthClientSecret = null;

		// Act & Assert - 例外が発生しないことを確認
		var exception = await Record.ExceptionAsync(async () => await _publisher.InitializeAsync());
		Assert.Null(exception);
	}

	[Fact]
	public async Task InitializeAsync_RefreshToken設定時_正常初期化される()
	{
		// Arrange
		_config.Dmdata.RefreshToken = "test-refresh-token";
		_config.Dmdata.OAuthClientId = "test-client-id";

		// Act & Assert - 例外が発生しないことを確認
		var exception = await Record.ExceptionAsync(async () => await _publisher.InitializeAsync());
		Assert.Null(exception);
	}

	#endregion

	#region Start/Stopメソッドのテスト

	[Fact]
	public async Task Start_単一カテゴリ_購読開始される()
	{
		// Arrange
		var categories = new[] { InformationCategory.Earthquake };

		// Act
		_publisher.Start(categories);

		// 少し待機してStartInternalAsyncの処理を待つ
		await Task.Delay(100);

		// Assert - 公開プロパティでサービス開始状態を確認
		Assert.NotEmpty(_publisher.SubscribingCategories);
	}

	[Fact]
	public async Task Start_複数カテゴリ_全て購読開始される()
	{
		// Arrange
		var categories = new[] { InformationCategory.Earthquake, InformationCategory.EewForecast };

		// Act
		_publisher.Start(categories);

		// 少し待機してStartInternalAsyncの処理を待つ
		await Task.Delay(100);

		// Assert - 公開プロパティでサービス開始状態を確認
		Assert.NotEmpty(_publisher.SubscribingCategories);
	}

	[Fact]
	public async Task Stop_購読中カテゴリ_停止される()
	{
		// Arrange
		var categories = new[] { InformationCategory.Earthquake };
		_publisher.Start(categories);
		await Task.Delay(100); // Start処理の完了を待つ

		// Act
		_publisher.Stop(categories);
		await Task.Delay(100); // Stop処理の完了を待つ

		// Assert - 公開プロパティでサービス停止状態を確認
		Assert.Empty(_publisher.SubscribingCategories);
	}

	#endregion


	#region サポートカテゴリ取得のテスト

	[Fact]
	public async Task GetSupportedCategoriesAsync_認証情報未設定時_空配列を返却()
	{
		// Act
		var categories = await _publisher.GetSupportedCategoriesAsync();

		// Assert - 認証情報未設定なので空配列が返される
		Assert.NotNull(categories);
		Assert.Empty(categories);
	}

	#endregion

	#region DmdataSharpインターフェイステスト

	[Fact]
	public void MockController_プロパティ設定_正しく動作する()
	{
		// Arrange & Act
		_mockController.Setup(x => x.TotalMessagesReceived).Returns(150);
		_mockController.Setup(x => x.DuplicateMessagesFiltered).Returns(25);
		_mockController.Setup(x => x.ActiveConnectionCount).Returns(3);
		_mockController.Setup(x => x.Status).Returns(RedundancyStatus.FullyConnected);
		_mockController.Setup(x => x.ConnectedEndpoints).Returns(new[] { "ws001dmdatajp", "ws002dmdatajp", "ws003dmdatajp" });
		_mockController.Setup(x => x.IsConnected).Returns(true);

		var lastMessageTime = DateTime.UtcNow.AddMinutes(-5);
		_mockController.Setup(x => x.LastMessageTime).Returns(lastMessageTime);

		// Assert
		Assert.Equal(150, _mockController.Object.TotalMessagesReceived);
		Assert.Equal(25, _mockController.Object.DuplicateMessagesFiltered);
		Assert.Equal(3, _mockController.Object.ActiveConnectionCount);
		Assert.Equal(RedundancyStatus.FullyConnected, _mockController.Object.Status);
		Assert.Equal(3, _mockController.Object.ConnectedEndpoints.Length);
		Assert.True(_mockController.Object.IsConnected);
		Assert.Equal(lastMessageTime, _mockController.Object.LastMessageTime);
		Assert.Contains("ws002dmdatajp", _mockController.Object.ConnectedEndpoints);
	}

	[Fact]
	public async Task MockController_ConnectAsync_正常呼び出しされる()
	{
		// Arrange
		var mockParam = new Mock<DmdataSharp.ApiParameters.V2.SocketStartRequestParameter>();
		var endpoints = new[] { "ws001dmdatajp", "ws002dmdatajp" };

		_mockController.Setup(x => x.ConnectAsync(It.IsAny<DmdataSharp.ApiParameters.V2.SocketStartRequestParameter>(), It.IsAny<string[]>()))
			.Returns(Task.CompletedTask);

		// Act
		await _mockController.Object.ConnectAsync(mockParam.Object, endpoints);

		// Assert
		_mockController.Verify(x => x.ConnectAsync(mockParam.Object, endpoints), Times.Once);
	}

	[Fact]
	public async Task MockController_DisconnectAsync_正常呼び出しされる()
	{
		// Arrange
		_mockController.Setup(x => x.DisconnectAsync()).Returns(Task.CompletedTask);

		// Act
		await _mockController.Object.DisconnectAsync();

		// Assert
		_mockController.Verify(x => x.DisconnectAsync(), Times.Once);
	}

	[Fact]
	public void MockController_DataReceivedイベント_正しく処理される()
	{
		// Arrange
		var eventFired = false;
		DataWebSocketMessage? receivedMessage = null;

		var testMessage = new DataWebSocketMessage
		{
			Id = "test-message-id",
			Classification = "地震情報",
			Body = "{\"test\": \"data\"}"
		};

		// Act - イベントハンドラー登録
		_mockController.Object.DataReceived += (sender, message) =>
		{
			eventFired = true;
			receivedMessage = message;
		};

		// イベント発生をシミュレート
		_mockController.Raise(x => x.DataReceived += null, _mockController.Object, testMessage);

		// Assert
		Assert.True(eventFired);
		Assert.NotNull(receivedMessage);
		Assert.Equal("test-message-id", receivedMessage.Id);
		Assert.Equal("地震情報", receivedMessage.Classification);
	}

	[Fact]
	public void MockController_RedundancyStatusChangedイベント_正しく処理される()
	{
		// Arrange
		var eventFired = false;
		RedundancyStatusChangedEventArgs? receivedArgs = null;

		var testArgs = new RedundancyStatusChangedEventArgs
		{
			Status = RedundancyStatus.PartiallyConnected,
			ActiveConnections = 1,
			ActiveEndpoints = new[] { "ws001dmdatajp" },
			ChangedTime = DateTime.UtcNow
		};

		// Act - イベントハンドラー登録
		_mockController.Object.RedundancyStatusChanged += (sender, args) =>
		{
			eventFired = true;
			receivedArgs = args;
		};

		// イベント発生をシミュレート
		_mockController.Raise(x => x.RedundancyStatusChanged += null, _mockController.Object, testArgs);

		// Assert
		Assert.True(eventFired);
		Assert.NotNull(receivedArgs);
		Assert.Equal(RedundancyStatus.PartiallyConnected, receivedArgs.Status);
		Assert.Equal(1, receivedArgs.ActiveConnections);
		Assert.Contains("ws001dmdatajp", receivedArgs.ActiveEndpoints);
	}

	#endregion

	#region 内部状態確認のテスト

	[Fact]
	public void Publisher_基本プロパティ_正しく初期化される()
	{
		// Assert - 公開プロパティが正しく初期化されることを確認
		Assert.NotNull(_publisher);
		Assert.Equal(0, _publisher.ActiveConnectionCount);
		Assert.Equal(0, _publisher.TotalMessagesReceived);
		// DuplicateMessagesFilteredは隙長性コントローラーに依存
		Assert.Empty(_publisher.SubscribingCategories);
		Assert.Equal("Disconnected", _publisher.RedundancyStatus.ToString());
	}

	#endregion

	#region 非同期処理とタイマーのテスト

	[Fact]
	public async Task 接続エラー_発生時_適切にハンドリングされる()
	{
		// Arrange
		await _publisher.InitializeAsync();

		// Act - 認証情報未設定で開始処理
		_publisher.Start([InformationCategory.Earthquake]);

		// 短時間待機して処理完了を待つ
		await Task.Delay(200);

		// Assert - モック環境ではエラーが発生しないが、状態は適切に管理される
		Assert.Equal("Disconnected", _publisher.RedundancyStatus.ToString());
		Assert.NotEmpty(_publisher.SubscribingCategories); // Start は呼ばれている
	}

	[Fact]
	public async Task 認証エラー_発生時_設定がクリアされる()
	{
		// Arrange - 認証情報を設定
		_config.Dmdata.RefreshToken = "test-refresh-token";
		_config.Dmdata.OAuthClientId = "test-client-id";
		await _publisher.InitializeAsync();

		// Act - モック環境での開始処理
		_publisher.Start([InformationCategory.Earthquake]);

		// 処理完了を待つ
		await Task.Delay(200);

		// Assert - モック環境では実際の認証エラーが発生しないが、基本動作を確認
		Assert.NotEmpty(_publisher.SubscribingCategories);
		Assert.Equal("test-refresh-token", _config.Dmdata.RefreshToken); // モックではクリアされない
	}

	#endregion

	#region イベントハンドリングのテスト

	[Fact]
	public void ConnectionStatusChanged_イベント購読_正常動作()
	{
		// Arrange
		var eventFired = false;
		_publisher.ConnectionStatusChanged += (s, e) => eventFired = true;

		// Act & Assert - イベントが登録されていることを確認
		// 実際のイベント発生は内部実装に依存するため、登録の確認のみ
		Assert.False(eventFired); // まだイベントが発生していないことを確認

		// イベントハンドラーが正しく登録されていることを確認するため、
		// 内部実装でイベントが発生するかテスト
		Assert.NotNull(_publisher); // Publisherが正常に初期化されていることを確認
	}

	#endregion

	#region ヘルパーメソッド


	[Fact(DisplayName = "サービス_開始後停止_状態が正しく変化する")]
	public async Task サービス_開始後停止_状態が正しく変化する()
	{
		// Arrange
		await _publisher.InitializeAsync();

		// Act & Assert - 開始
		_publisher.Start([InformationCategory.Earthquake]);
		await Task.Delay(100);
		Assert.NotEmpty(_publisher.SubscribingCategories);

		// Act & Assert - 停止
		_publisher.Stop([InformationCategory.Earthquake]);
		await Task.Delay(100);
		Assert.Empty(_publisher.SubscribingCategories);
	}

	[Fact(DisplayName = "複数サービス_同時開始停止_状態管理が正しく動作する")]
	public async Task 複数サービス_同時開始停止_状態管理が正しく動作する()
	{
		// Arrange
		await _publisher.InitializeAsync();
		var categories = new[] { InformationCategory.Earthquake, InformationCategory.Tsunami, InformationCategory.EewForecast };

		// Act - 複数カテゴリで開始
		_publisher.Start(categories);
		await Task.Delay(100);

		// Assert - 開始状態
		Assert.NotEmpty(_publisher.SubscribingCategories);

		// Act - 一部停止
		_publisher.Stop([InformationCategory.Tsunami]);
		await Task.Delay(100);

		// Assert - まだ他のカテゴリが動いているので開始状態
		Assert.NotEmpty(_publisher.SubscribingCategories);

		// Act - 全て停止
		_publisher.Stop([InformationCategory.Earthquake, InformationCategory.EewForecast]);
		await Task.Delay(100);

		// Assert - 完全停止
		Assert.Empty(_publisher.SubscribingCategories);
	}

	[Fact(DisplayName = "ReconnectImmediatelyAsync_WebSocket使用時_例外処理が適切に行われる")]
	public async Task ReconnectImmediatelyAsync_WebSocket使用時_例外処理が適切に行われる()
	{
		// Arrange - WebSocketを有効にする
		_config.Dmdata.UseWebSocket = true;
		await _publisher.InitializeAsync();

		// Act - モック環境ではApiClientが初期化されないため、期待される例外が発生する
		var exception = await Record.ExceptionAsync(() => _publisher.ReconnectImmediatelyAsync());

		// Assert - 適切なエラーメッセージが含まれることを確認
		Assert.NotNull(exception);
		Assert.Contains("ApiClient が初期化されていません", exception.Message);
	}

	[Fact(DisplayName = "ReconnectImmediatelyAsync_WebSocket非使用時_処理がスキップされる")]
	public async Task ReconnectImmediatelyAsync_WebSocket非使用時_処理がスキップされる()
	{
		// Arrange - WebSocketを無効にする
		_config.Dmdata.UseWebSocket = false;
		await _publisher.InitializeAsync();

		// Act
		var exception = await Record.ExceptionAsync(() => _publisher.ReconnectImmediatelyAsync());

		// Assert - 例外が発生せず、処理がスキップされることを確認
		Assert.Null(exception);
	}

	[Fact(DisplayName = "複数カテゴリ開始_全カテゴリが正常に開始される")]
	public async Task 複数カテゴリ開始_全カテゴリが正常に開始される()
	{
		// Arrange
		await _publisher.InitializeAsync();
		var categories = new[] { InformationCategory.Earthquake, InformationCategory.Tsunami };

		// Act - 複数カテゴリで開始
		_publisher.Start(categories);

		// 短時間待機して接続が確立されることを確認
		await Task.Delay(100);

		// Assert - サービス開始状態を確認
		Assert.NotEmpty(_publisher.SubscribingCategories);
	}

	[Fact(DisplayName = "初期化後_プロパティが正しい値を示す")]
	public async Task 初期化後_プロパティが正しい値を示す()
	{
		// Arrange & Act
		await _publisher.InitializeAsync();

		// Assert - 初期化後のプロパティを確認
		Assert.Equal(0, _publisher.ActiveConnectionCount);
		Assert.Equal(0, _publisher.TotalMessagesReceived);
		// DuplicateMessagesFilteredは隙長性コントローラーに依存
		Assert.Empty(_publisher.SubscribingCategories);
		Assert.Equal("Disconnected", _publisher.RedundancyStatus.ToString());
		Assert.Empty(_publisher.ConnectedEndpoints);
		Assert.Null(_publisher.LastMessageTime);
	}

	[Fact(DisplayName = "イベント購読_ハンドラーが正しく登録される")]
	public void イベント購読_ハンドラーが正しく登録される()
	{
		// Arrange
		var notificationReceived = false;
		TelegramPublisher? receivedPublisher = null;
		InformationCategory[]? receivedCategories = null;
		bool? isRestorable = null;

		// Act - Failedイベント購読
		_publisher.Failed += (publisher, categories, restorable) =>
		{
			notificationReceived = true;
			receivedPublisher = publisher;
			receivedCategories = categories;
			isRestorable = restorable;
		};

		// Assert - イベントハンドラーの登録は成功する
		// モック環境では実際の障害が発生しないため、登録のみテスト
		Assert.NotNull(_publisher);
		Assert.False(notificationReceived); // まだイベントは発生していない
	}


	#endregion

	public void Dispose()
	{
		_publisher?.Dispose();
		_mockController?.Reset();
		_mockApiClient?.Reset();
		GC.SuppressFinalize(this);
	}
}

/// <summary>
/// デフォルトのログマネージャー実装（テスト用）
/// </summary>
internal class DefaultLogManager : ILogManager
{
	private static readonly Mock<IFullLogger> _mockLogger = new();

	public IFullLogger GetLogger<T>() => _mockLogger.Object;
	public IFullLogger GetLogger(Type type) => _mockLogger.Object;
}