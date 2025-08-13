using KyoshinEewViewer.Services;
using KyoshinEewViewer.Services.TelegramPublishers;
using KyoshinEewViewer.Tests.Services.Mocks;
using Moq;
using Splat;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace KyoshinEewViewer.Tests.Services;

/// <summary>
/// TelegramProvideServiceのエラーハンドリングテスト
/// </summary>
public class TelegramProvideServiceErrorHandlingTests : IDisposable
{
	private readonly Mock<ILogManager> _mockLogManager;
	private readonly Mock<IFullLogger> _mockLogger;
	private readonly Mock<IReadonlyDependencyResolver> _mockServiceProvider;
	private readonly TelegramProvideService _service;

public TelegramProvideServiceErrorHandlingTests()
	{
		_mockLogManager = new Mock<ILogManager>();
		_mockLogger = new Mock<IFullLogger>();
		
		// GetLoggerメソッドを適切にセットアップするために、実際の実装を提供
		_mockLogManager.Setup(x => x.GetLogger(It.IsAny<Type>()))
			.Returns(_mockLogger.Object);

		_mockServiceProvider = new Mock<IReadonlyDependencyResolver>();

		_service = new TelegramProvideService(_mockLogManager.Object, _mockServiceProvider.Object);
	}

	[Fact(DisplayName = "サブスクライバーの到着コールバックで例外が発生しても処理を継続する")]
	public async Task SubscriberArrivedCallback_ThrowsException_ShouldContinueProcessing()
	{
		// Arrange
		var publisher = new MockTelegramPublisher
		{
			Name = "ErrorPublisher",
			SupportedCategories = [InformationCategory.Earthquake]
		};

		var customTypes = new[] { typeof(MockTelegramPublisher) };

		_mockServiceProvider.Setup(x => x.GetService(typeof(MockTelegramPublisher), null))
			.Returns(publisher);

		var goodSubscriberCalled = false;
		var telegram1 = new MockTelegram("test1", "テスト1", "T001", DateTime.Now);

		// 異常なサブスクライバー（例外を投げる）
		_service.Subscribe(
			InformationCategory.Earthquake,
			(_, _) => Task.CompletedTask,
			arrived: _ => throw new InvalidOperationException("テストエラー"),
			failed: _ => { }
		);

		// 正常なサブスクライバー
		_service.Subscribe(
			InformationCategory.Earthquake,
			(_, _) => Task.CompletedTask,
			arrived: telegram =>
			{
				goodSubscriberCalled = true;
				return Task.CompletedTask;
			},
			failed: _ => { }
		);

		await _service.StartAsync(customTypes);

		// Act
		publisher.SimulateTelegramArrival(InformationCategory.Earthquake, telegram1);
		await Task.Delay(200);

		// Assert
		Assert.True(goodSubscriberCalled, "例外が発生しても他のサブスクライバーは処理されること");
		
		// エラーがログに記録されていることを確認
		_mockLogger.Verify(
			x => x.Write(It.IsAny<string>(), It.IsAny<LogLevel>()),
			Times.AtLeastOnce
		);
	}

	[Fact(DisplayName = "サブスクライバーのソース切替コールバックで例外が発生しても処理を継続する")]
	public async Task SubscriberSourceSwitchedCallback_ThrowsException_ShouldContinueProcessing()
	{
		// Arrange
		var publisher = new MockTelegramPublisher
		{
			Name = "SourceSwitchErrorPublisher",
			SupportedCategories = [InformationCategory.Earthquake]
		};

		var customTypes = new[] { typeof(MockTelegramPublisher) };

		_mockServiceProvider.Setup(x => x.GetService(typeof(MockTelegramPublisher), null))
			.Returns(publisher);

		var goodSubscriberCalled = false;
		var historyTelegrams = new[]
		{
			new MockTelegram("hist1", "履歴1", "H001", DateTime.Now)
		};

		// 異常なサブスクライバー（例外を投げる）
		_service.Subscribe(
			InformationCategory.Earthquake,
			sourceSwitched: (_, _) => throw new InvalidOperationException("SourceSwitchedエラー"),
			arrived: _ => Task.CompletedTask,
			failed: _ => { }
		);

		// 正常なサブスクライバー
		_service.Subscribe(
			InformationCategory.Earthquake,
			sourceSwitched: (name, telegrams) =>
			{
				goodSubscriberCalled = true;
				return Task.CompletedTask;
			},
			arrived: _ => Task.CompletedTask,
			failed: _ => { }
		);

		await _service.StartAsync(customTypes);

		// Act
		publisher.SimulateHistoryTelegramArrival("SourceSwitchErrorPublisher", InformationCategory.Earthquake, historyTelegrams);
		await Task.Delay(200);

		// Assert
		Assert.True(goodSubscriberCalled, "例外が発生しても他のサブスクライバーは処理されること");
		
		// エラーがログに記録されていることを確認
		_mockLogger.Verify(
			x => x.Write(It.IsAny<string>(), It.IsAny<LogLevel>()),
			Times.AtLeastOnce
		);
	}

	[Fact(DisplayName = "プロバイダの初期化で例外が発生した場合、スキップして継続する")]
	public async Task PublisherInitializationException_ShouldSkipAndContinue()
	{
		// Arrange
		var failingPublisher = new MockTelegramPublisher
		{
			Name = "FailingPublisher",
			ShouldFailOnInit = true
		};
		var workingPublisher = new MockTelegramPublisher
		{
			Name = "WorkingPublisher",
			SupportedCategories = [InformationCategory.Earthquake]
		};

		var customTypes = new[]
		{
			typeof(MockTelegramPublisher),
			typeof(MockTelegramPublisher)
		};

		_mockServiceProvider.SetupSequence(x => x.GetService(typeof(MockTelegramPublisher), null))
			.Returns(failingPublisher)
			.Returns(workingPublisher);

		// Act & Assert - 例外が発生しても正常に完了すること
		await _service.StartAsync(customTypes);

		Assert.False(failingPublisher.IsInitialized);
		Assert.True(workingPublisher.IsInitialized);
		
		// エラーがログに記録されていることを確認
		_mockLogger.Verify(
			x => x.Write(It.IsAny<string>(), It.IsAny<LogLevel>()),
			Times.AtLeastOnce
		);
	}

	[Fact(DisplayName = "サポートカテゴリ取得で例外が発生したプロバイダをスキップする")]
	public async Task GetSupportedCategoriesException_ShouldSkipPublisher()
	{
		// Arrange
		var failingPublisher = new MockTelegramPublisher
		{
			Name = "CategoryFailPublisher",
			ShouldFailOnGetCategories = true
		};
		var workingPublisher = new MockTelegramPublisher
		{
			Name = "WorkingPublisher",
			SupportedCategories = [InformationCategory.Earthquake]
		};

		var customTypes = new[]
		{
			typeof(MockTelegramPublisher),
			typeof(MockTelegramPublisher)
		};

		_mockServiceProvider.SetupSequence(x => x.GetService(typeof(MockTelegramPublisher), null))
			.Returns(failingPublisher)
			.Returns(workingPublisher);

		_service.Subscribe(
			InformationCategory.Earthquake,
			(_, _) => Task.CompletedTask,
			_ => Task.CompletedTask,
			_ => { }
		);

		// Act
		await _service.StartAsync(customTypes);

		// Assert
		// 正常なPublisherのみが処理を開始していること
		Assert.Contains(InformationCategory.Earthquake, workingPublisher.StartedCategories);
		Assert.Empty(failingPublisher.StartedCategories);
		
		// エラーがログに記録されていることを確認
		_mockLogger.Verify(
			x => x.Write(It.IsAny<string>(), It.IsAny<LogLevel>()),
			Times.AtLeastOnce
		);
	}

	[Fact(DisplayName = "RestoreAsyncでプロバイダ例外が発生しても他のプロバイダで継続する")]
	public async Task RestoreAsync_WithPublisherException_ShouldContinueWithOthers()
	{
		// Arrange
		var failingPublisher = new MockTelegramPublisher
		{
			Name = "RestoreFailPublisher",
			SupportedCategories = [InformationCategory.Earthquake],
			ShouldFailOnGetCategories = true // RestoreAsync内で失敗
		};
		var workingPublisher = new MockTelegramPublisher
		{
			Name = "WorkingPublisher",
			SupportedCategories = [InformationCategory.Tsunami]
		};

		var customTypes = new[]
		{
			typeof(MockTelegramPublisher),
			typeof(MockTelegramPublisher)
		};

		_mockServiceProvider.SetupSequence(x => x.GetService(typeof(MockTelegramPublisher), null))
			.Returns(failingPublisher)
			.Returns(workingPublisher);

		_service.Subscribe(
			InformationCategory.Earthquake,
			(_, _) => Task.CompletedTask,
			_ => Task.CompletedTask,
			_ => { }
		);
		_service.Subscribe(
			InformationCategory.Tsunami,
			(_, _) => Task.CompletedTask,
			_ => Task.CompletedTask,
			_ => { }
		);

		await _service.StartAsync(customTypes);

		// Act
		// 津波サポートを追加してRestore
		workingPublisher.SupportedCategories = [InformationCategory.Tsunami];
		failingPublisher.ShouldFailOnGetCategories = false; // 一旦修復
		failingPublisher.SupportedCategories = [InformationCategory.Earthquake];
		
		// Restore中に再度失敗するよう設定
		failingPublisher.ShouldFailOnGetCategories = true;
		
		await _service.RestoreAsync();

		// Assert
		// 正常なPublisherは影響を受けない
		Assert.Contains(InformationCategory.Tsunami, workingPublisher.StartedCategories);
		
		// エラーがログに記録されていることを確認
		_mockLogger.Verify(
			x => x.Write(It.IsAny<string>(), It.IsAny<LogLevel>()),
			Times.AtLeastOnce
		);
	}

	[Fact(DisplayName = "DIからnullプロバイダが返された場合、適切にスキップする")]
	public async Task NullPublisherFromDI_ShouldSkipGracefully()
	{
		// Arrange
		var workingPublisher = new MockTelegramPublisher
		{
			Name = "WorkingPublisher",
			SupportedCategories = [InformationCategory.Earthquake]
		};

		var customTypes = new[]
		{
			typeof(MockTelegramPublisher),
			typeof(MockTelegramPublisher)
		};

		_mockServiceProvider.SetupSequence(x => x.GetService(typeof(MockTelegramPublisher), null))
			.Returns((MockTelegramPublisher?)null) // 最初のPublisherがnull
			.Returns(workingPublisher);

		_service.Subscribe(
			InformationCategory.Earthquake,
			(_, _) => Task.CompletedTask,
			_ => Task.CompletedTask,
			_ => { }
		);

		// Act & Assert - 例外が発生しないこと
		await _service.StartAsync(customTypes);

		Assert.True(workingPublisher.IsInitialized);
		Assert.Contains(InformationCategory.Earthquake, workingPublisher.StartedCategories);
	}

	[Fact(DisplayName = "空のプロバイダタイプリストでも適切に完了する")]
	public async Task EmptyPublisherTypesList_ShouldCompleteGracefully()
	{
		// Arrange & Act
		await _service.StartAsync([]);

		// Assert - 例外が発生しないこと
		// Started プロパティはプライベートなので、例外が発生しないことで確認
		// テストが正常に完了すれば開始されたと判断
	}

	[Fact(DisplayName = "全てのプロバイダがnullでも適切に完了する")]
	public async Task AllPublishersNull_ShouldCompleteGracefully()
	{
		// Arrange
		var customTypes = new[]
		{
			typeof(MockTelegramPublisher),
			typeof(MockTelegramPublisher)
		};

		_mockServiceProvider.Setup(x => x.GetService(typeof(MockTelegramPublisher), null))
			.Returns((MockTelegramPublisher?)null);

		var failureReported = false;

		_service.Subscribe(
			InformationCategory.Earthquake,
			(_, _) => Task.CompletedTask,
			_ => Task.CompletedTask,
			state => failureReported = true
		);

		// Act
		await _service.StartAsync(customTypes);

		// Assert
		// Started プロパティはプライベートなので、例外が発生しないことで確認
		// テストが正常に完了すれば開始されたと判断
		Assert.True(failureReported, "すべてのPublisherが利用できない場合は失敗が報告されること");
	}

	[Fact(DisplayName = "再帰的な失敗処理でもスタックオーバーフローを起こさない")]
	public async Task RecursiveFailureHandling_ShouldNotCauseStackOverflow()
	{
		// Arrange
		var problematicPublisher = new MockTelegramPublisher
		{
			Name = "ProblematicPublisher",
			SupportedCategories = [InformationCategory.Earthquake]
		};

		var customTypes = new[] { typeof(MockTelegramPublisher) };

		_mockServiceProvider.Setup(x => x.GetService(typeof(MockTelegramPublisher), null))
			.Returns(problematicPublisher);

		var failureCount = 0;

		_service.Subscribe(
			InformationCategory.Earthquake,
			(_, _) => Task.CompletedTask,
			_ => Task.CompletedTask,
			state => failureCount++
		);

		await _service.StartAsync(customTypes);

		// Act - 複数回の失敗を短時間で発生させる
		for (int i = 0; i < 10; i++)
		{
			problematicPublisher.SimulateFailure([InformationCategory.Earthquake], false);
		}

		await Task.Delay(500);

		// Assert - スタックオーバーフローが発生しないこと
		Assert.True(failureCount > 0, "失敗が報告されること");
		// 過剰な失敗報告がないことを確認（重複除去が機能している）
		Assert.True(failureCount <= 10, "適切な回数の失敗報告であること");
	}

	[Fact(DisplayName = "不正な電文データを適切に処理する")]
	public async Task MalformedTelegramData_ShouldHandleGracefully()
	{
		// Arrange
		var publisher = new MockTelegramPublisher
		{
			Name = "MalformedPublisher",
			SupportedCategories = [InformationCategory.Earthquake]
		};

		var customTypes = new[] { typeof(MockTelegramPublisher) };

		_mockServiceProvider.Setup(x => x.GetService(typeof(MockTelegramPublisher), null))
			.Returns(publisher);

		var receivedTelegrams = new List<Telegram>();

		_service.Subscribe(
			InformationCategory.Earthquake,
			(_, _) => Task.CompletedTask,
			telegram =>
			{
				receivedTelegrams.Add(telegram);
				return Task.CompletedTask;
			},
			_ => { }
		);

		await _service.StartAsync(customTypes);

		// Act - 異常なTelegramデータを送信
		var malformedTelegram = new MockTelegram("", "", "", DateTime.MinValue); // 空の値
		publisher.SimulateTelegramArrival(InformationCategory.Earthquake, malformedTelegram);
		
		await Task.Delay(100);

		// Assert - 異常データでも処理が継続されること
		Assert.Single(receivedTelegrams);
		Assert.Equal(malformedTelegram, receivedTelegrams[0]);
	}

	public void Dispose()
	{
		_service?.Dispose();
		GC.SuppressFinalize(this);
	}
}
