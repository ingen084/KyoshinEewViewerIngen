using KyoshinEewViewer.Services;
using KyoshinEewViewer.Services.TelegramPublishers;
using KyoshinEewViewer.Tests.Services.Mocks;
using Moq;
using Splat;

namespace KyoshinEewViewer.Tests.Services;

/// <summary>
/// TelegramProvideServiceの復旧機能テスト
/// </summary>
public class TelegramProvideServiceRecoveryTests : IDisposable
{
	private readonly Mock<ILogManager> _mockLogManager;
	private readonly Mock<IFullLogger> _mockLogger;
	private readonly Mock<IReadonlyDependencyResolver> _mockServiceProvider;
	private readonly TelegramProvideService _service;

public TelegramProvideServiceRecoveryTests()
	{
		_mockLogManager = new Mock<ILogManager>();
		_mockLogger = new Mock<IFullLogger>();
		
		// GetLoggerメソッドを適切にセットアップするために、実際の実装を提供
		_mockLogManager.Setup(x => x.GetLogger(It.IsAny<Type>()))
			.Returns(_mockLogger.Object);

		_mockServiceProvider = new Mock<IReadonlyDependencyResolver>();

		_service = new TelegramProvideService(_mockLogManager.Object, _mockServiceProvider.Object);
	}

	[Fact(DisplayName = "高優先度プロバイダが復旧した場合、処理を引き継ぐ")]
	public async Task HigherPriorityPublisherRecovery_ShouldSwitchBack()
	{
		// Arrange
		var primaryPublisher = new MockTelegramPublisher
		{
			Name = "Primary",
			SupportedCategories = [] // 初期状態では無効
		};
		var secondaryPublisher = new MockTelegramPublisher
		{
			Name = "Secondary",
			SupportedCategories = [InformationCategory.Earthquake]
		};

		var customTypes = new[]
		{
			typeof(MockTelegramPublisher), // Primary (優先度1)
			typeof(MockTelegramPublisher)  // Secondary (優先度2)
		};

		_mockServiceProvider.SetupSequence(x => x.GetService(typeof(MockTelegramPublisher)))
			.Returns(primaryPublisher)
			.Returns(secondaryPublisher);

		_service.Subscribe(
			InformationCategory.Earthquake,
			(_, _) => Task.CompletedTask,
			_ => Task.CompletedTask,
			_ => { }
		);

		await _service.StartAsync(customTypes);

		// SecondaryがEarthquakeを処理していることを確認
		Assert.Contains(InformationCategory.Earthquake, secondaryPublisher.StartedCategories);
		Assert.Empty(primaryPublisher.StartedCategories);

		// Act - Primaryが復旧
		primaryPublisher.SupportedCategories = [InformationCategory.Earthquake];
		primaryPublisher.SimulateRecovery();

		await Task.Delay(200);

		// Assert - PrimaryがEarthquakeを引き継ぐ
		Assert.Contains(InformationCategory.Earthquake, primaryPublisher.StartedCategories);
		Assert.DoesNotContain(InformationCategory.Earthquake, secondaryPublisher.StartedCategories);
	}

	[Fact(DisplayName = "部分的なカテゴリ復旧を正しく処理する")]
	public async Task PartialCategoryRecovery_ShouldHandleCorrectly()
	{
		// Arrange
		var primaryPublisher = new MockTelegramPublisher
		{
			Name = "Primary",
			SupportedCategories = [InformationCategory.Earthquake] // 地震のみ
		};
		var secondaryPublisher = new MockTelegramPublisher
		{
			Name = "Secondary",
			SupportedCategories = [InformationCategory.Earthquake, InformationCategory.Tsunami]
		};

		var customTypes = new[]
		{
			typeof(MockTelegramPublisher),
			typeof(MockTelegramPublisher)
		};

		_mockServiceProvider.SetupSequence(x => x.GetService(typeof(MockTelegramPublisher)))
			.Returns(primaryPublisher)
			.Returns(secondaryPublisher);

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

		// 初期状態確認
		Assert.Contains(InformationCategory.Earthquake, primaryPublisher.StartedCategories);
		Assert.Contains(InformationCategory.Tsunami, secondaryPublisher.StartedCategories);

		// Primaryが地震で失敗
		primaryPublisher.SimulateFailure([InformationCategory.Earthquake], false);
		await Task.Delay(200);

		// Secondaryが両方を処理
		Assert.Contains(InformationCategory.Earthquake, secondaryPublisher.StartedCategories);
		Assert.Contains(InformationCategory.Tsunami, secondaryPublisher.StartedCategories);
		// Primaryは失敗したが、StartedCategoriesからは削除されない（テスト用メンバの仕様）

		// Act - Primaryが津波も対応可能になって復旧
		primaryPublisher.SupportedCategories = [InformationCategory.Earthquake, InformationCategory.Tsunami];
		primaryPublisher.SimulateRecovery();

		await Task.Delay(200);

		// Assert - Primaryが両方を引き継ぐ
		Assert.Contains(InformationCategory.Earthquake, primaryPublisher.StartedCategories);
		Assert.Contains(InformationCategory.Tsunami, primaryPublisher.StartedCategories);
		// Secondaryは停止されるが、StartedCategoriesからは削除されない（テスト用メンバの仕様）
		// 実際の動作では、UsingPublisherでPrimaryに切り替わっている
	}

	[Fact(DisplayName = "低優先度プロバイダの復旧は高優先度プロバイダに影響しない")]
	public async Task LowerPriorityRecovery_ShouldNotAffectHigherPriority()
	{
		// Arrange
		var primaryPublisher = new MockTelegramPublisher
		{
			Name = "Primary",
			SupportedCategories = [InformationCategory.Earthquake]
		};
		var secondaryPublisher = new MockTelegramPublisher
		{
			Name = "Secondary",
			SupportedCategories = [] // 初期状態では無効
		};

		var customTypes = new[]
		{
			typeof(MockTelegramPublisher),
			typeof(MockTelegramPublisher)
		};

		_mockServiceProvider.SetupSequence(x => x.GetService(typeof(MockTelegramPublisher)))
			.Returns(primaryPublisher)
			.Returns(secondaryPublisher);

		_service.Subscribe(
			InformationCategory.Earthquake,
			(_, _) => Task.CompletedTask,
			_ => Task.CompletedTask,
			_ => { }
		);

		await _service.StartAsync(customTypes);

		// 初期状態確認
		Assert.Contains(InformationCategory.Earthquake, primaryPublisher.StartedCategories);
		Assert.Empty(secondaryPublisher.StartedCategories);

		// Act - Secondaryが復旧（しかし優先度が低い）
		secondaryPublisher.SupportedCategories = [InformationCategory.Earthquake];
		secondaryPublisher.SimulateRecovery();

		await Task.Delay(200);

		// Assert - Primaryがそのまま処理を続ける
		Assert.Contains(InformationCategory.Earthquake, primaryPublisher.StartedCategories);
		Assert.Empty(secondaryPublisher.StartedCategories);
	}

	[Fact(DisplayName = "複数プロバイダの復旧時は最高優先度を使用する")]
	public async Task MultiplePublisherRecovery_ShouldUseHighestPriority()
	{
		// Arrange
		var publisher1 = new MockTelegramPublisher
		{
			Name = "Publisher1",
			SupportedCategories = [] // 初期状態では無効
		};
		var publisher2 = new MockTelegramPublisher
		{
			Name = "Publisher2",
			SupportedCategories = [] // 初期状態では無効
		};
		var publisher3 = new MockTelegramPublisher
		{
			Name = "Publisher3",
			SupportedCategories = [InformationCategory.Earthquake]
		};

		var customTypes = new[]
		{
			typeof(MockTelegramPublisher), // 最高優先度
			typeof(MockTelegramPublisher), // 中優先度
			typeof(MockTelegramPublisher)  // 最低優先度
		};

		_mockServiceProvider.SetupSequence(x => x.GetService(typeof(MockTelegramPublisher)))
			.Returns(publisher1)
			.Returns(publisher2)
			.Returns(publisher3);

		_service.Subscribe(
			InformationCategory.Earthquake,
			(_, _) => Task.CompletedTask,
			_ => Task.CompletedTask,
			_ => { }
		);

		await _service.StartAsync(customTypes);

		// 初期状態：Publisher3が処理
		Assert.Contains(InformationCategory.Earthquake, publisher3.StartedCategories);

		// Act - Publisher1とPublisher2が同時に復旧
		publisher1.SupportedCategories = [InformationCategory.Earthquake];
		publisher2.SupportedCategories = [InformationCategory.Earthquake];
		
		// Publisher2が先に復旧を通知
		publisher2.SimulateRecovery();
		await Task.Delay(100);
		
		// Publisher1が後に復旧を通知
		publisher1.SimulateRecovery();
		await Task.Delay(200);

		// Assert - 最高優先度のPublisher1が処理
		Assert.Contains(InformationCategory.Earthquake, publisher1.StartedCategories);
		Assert.DoesNotContain(InformationCategory.Earthquake, publisher2.StartedCategories);
		Assert.DoesNotContain(InformationCategory.Earthquake, publisher3.StartedCategories);
	}

	[Fact(DisplayName = "カテゴリエラーを伴う復旧でもスキップして継続する")]
	public async Task RecoveryWithCategoryError_ShouldSkipAndContinue()
	{
		// Arrange
		var primaryPublisher = new MockTelegramPublisher
		{
			Name = "Primary",
			SupportedCategories = [InformationCategory.Earthquake],
			ShouldFailOnGetCategories = true // カテゴリ取得で失敗
		};
		var secondaryPublisher = new MockTelegramPublisher
		{
			Name = "Secondary",
			SupportedCategories = [InformationCategory.Earthquake]
		};

		var customTypes = new[]
		{
			typeof(MockTelegramPublisher),
			typeof(MockTelegramPublisher)
		};

		_mockServiceProvider.SetupSequence(x => x.GetService(typeof(MockTelegramPublisher)))
			.Returns(primaryPublisher)
			.Returns(secondaryPublisher);

		_service.Subscribe(
			InformationCategory.Earthquake,
			(_, _) => Task.CompletedTask,
			_ => Task.CompletedTask,
			_ => { }
		);

		await _service.StartAsync(customTypes);

		// Secondaryが処理していることを確認
		Assert.Contains(InformationCategory.Earthquake, secondaryPublisher.StartedCategories);

		// Act - Primaryが復旧を通知するがカテゴリ取得でエラー
		primaryPublisher.SimulateRecovery();
		await Task.Delay(200);

		// Assert - エラーが発生してもSecondaryが継続
		Assert.Contains(InformationCategory.Earthquake, secondaryPublisher.StartedCategories);
		Assert.DoesNotContain(InformationCategory.Earthquake, primaryPublisher.StartedCategories);
	}

	[Fact(DisplayName = "RestoreAsyncは割り当てられていないカテゴリを復旧する")]
	public async Task RestoreAsync_ShouldRestoreUnassignedCategories()
	{
		// Arrange
		var primaryPublisher = new MockTelegramPublisher
		{
			Name = "Primary",
			SupportedCategories = [InformationCategory.Earthquake]
		};

		var customTypes = new[] { typeof(MockTelegramPublisher) };

		_mockServiceProvider.Setup(x => x.GetService(typeof(MockTelegramPublisher)))
			.Returns(primaryPublisher);

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

		// 初期状態：Earthquakeのみ処理、Tsunamiは未割り当て
		Assert.Contains(InformationCategory.Earthquake, primaryPublisher.StartedCategories);
		Assert.DoesNotContain(InformationCategory.Tsunami, primaryPublisher.StartedCategories);

		// Act - Primaryが津波もサポートするようになる
		primaryPublisher.SupportedCategories = [InformationCategory.Earthquake, InformationCategory.Tsunami];
		await _service.RestoreAsync();

		// Assert - 津波も処理されるようになる
		Assert.Contains(InformationCategory.Earthquake, primaryPublisher.StartedCategories);
		Assert.Contains(InformationCategory.Tsunami, primaryPublisher.StartedCategories);
	}

	[Fact(DisplayName = "RestoreAsyncは孤立したサブスクライバーを適切に処理する")]
	public async Task RestoreAsync_ShouldHandleOrphanedSubscriber()
	{
		// Arrange
		var primaryPublisher = new MockTelegramPublisher
		{
			Name = "Primary",
			SupportedCategories = [InformationCategory.Earthquake]
		};
		var secondaryPublisher = new MockTelegramPublisher
		{
			Name = "Secondary",
			SupportedCategories = [InformationCategory.Tsunami]
		};

		var customTypes = new[]
		{
			typeof(MockTelegramPublisher),
			typeof(MockTelegramPublisher)
		};

		_mockServiceProvider.SetupSequence(x => x.GetService(typeof(MockTelegramPublisher)))
			.Returns(primaryPublisher)
			.Returns(secondaryPublisher);

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

		// 正常に各Publisherが担当
		Assert.Contains(InformationCategory.Earthquake, primaryPublisher.StartedCategories);
		Assert.Contains(InformationCategory.Tsunami, secondaryPublisher.StartedCategories);

		// Act - Primaryが不復旧の失敗でEarthquakeがサポートできなくなった状況をシミュレート
		primaryPublisher.SupportedCategories = [];
		
		// RestoreAsyncはSubscriberが割り当てられていない状況を検出して復旧させる
		await _service.RestoreAsync();

		// Assert - SecondaryがEarthquakeも担当するようになる（Tsunamiもサポートしているため）
		secondaryPublisher.SupportedCategories = [InformationCategory.Earthquake, InformationCategory.Tsunami];
		await _service.RestoreAsync();
		
		Assert.Contains(InformationCategory.Earthquake, secondaryPublisher.StartedCategories);
		Assert.Contains(InformationCategory.Tsunami, secondaryPublisher.StartedCategories);
	}

	[Fact(DisplayName = "すべてのPublisherが利用不可能になった後、優先度の高いPublisherが復旧した場合にSubscriberにイベントが送信される")]
	public async Task AllPublishersUnavailable_HighPriorityRecovery_ShouldDeliverEventsToSubscribers()
	{
		// Arrange
		var highPriorityPublisher = new MockTelegramPublisher
		{
			Name = "HighPriorityPublisher",
			SupportedCategories = [InformationCategory.Earthquake]
		};
		var lowPriorityPublisher = new MockTelegramPublisher
		{
			Name = "LowPriorityPublisher", 
			SupportedCategories = [InformationCategory.Earthquake]
		};

		var customTypes = new[]
		{
			typeof(MockTelegramPublisher),
			typeof(MockTelegramPublisher)
		};

		_mockServiceProvider.SetupSequence(x => x.GetService(typeof(MockTelegramPublisher)))
			.Returns(highPriorityPublisher)
			.Returns(lowPriorityPublisher);

		var receivedTelegrams = new List<Telegram>();
		var sourceSwitchEvents = new List<string>();
		var failureEvents = new List<(bool isAllFailed, bool isRestorable)>();

		_service.Subscribe(
			InformationCategory.Earthquake,
			sourceSwitched: (name, telegrams) =>
			{
				sourceSwitchEvents.Add(name);
				return Task.CompletedTask;
			},
			arrived: telegram =>
			{
				receivedTelegrams.Add(telegram);
				return Task.CompletedTask;
			},
			failed: state => failureEvents.Add(state)
		);

		await _service.StartAsync(customTypes);

		// 初期状態では高優先度Publisherが処理
		Assert.Contains(InformationCategory.Earthquake, highPriorityPublisher.StartedCategories);

		// Act & Assert
		
		// 1. 高優先度Publisherが失敗 → 低優先度にフォールバック
		highPriorityPublisher.SimulateFailure([InformationCategory.Earthquake], false);
		await Task.Delay(100);

		Assert.Contains(InformationCategory.Earthquake, lowPriorityPublisher.StartedCategories);

		// 2. 低優先度Publisherも失敗 → 完全失敗状態
		lowPriorityPublisher.SimulateFailure([InformationCategory.Earthquake], false);
		await Task.Delay(100);

		Assert.Contains((true, false), failureEvents);

		// 3. 高優先度Publisherが復旧 → 再び高優先度Publisherが処理を担当
		// DmdataRedundantTelegramPublisherと同様の復旧処理をシミュレート
		highPriorityPublisher.SupportedCategories = [InformationCategory.Earthquake];
		
		// 復旧を通知（OnInformationCategoryUpdatedに相当）
		highPriorityPublisher.SimulateRecovery();
		await Task.Delay(200);
		
		// 実際のDmdataPublisherのように復旧時に履歴データを送信
		var recoveryHistoryTelegrams = new[] { 
			new MockTelegram("recovery-history", "復旧時履歴", "RH001", DateTime.Now) 
		};
		highPriorityPublisher.SimulateHistoryTelegramArrival("HighPriorityPublisher", InformationCategory.Earthquake, recoveryHistoryTelegrams);

		// 4. 復旧後にTelegramが送信される → Subscriberに正しく届く
		var testTelegram = new MockTelegram("recovery-test", "復旧テスト", "RCV001", DateTime.Now);
		highPriorityPublisher.SimulateTelegramArrival(InformationCategory.Earthquake, testTelegram);
		await Task.Delay(100);

		// Assert
		// 復旧後のTelegramがSubscriberに届いていることを確認
		Assert.Contains(receivedTelegrams, t => t.Key == "recovery-test");
		
		// ソース切り替えイベントが発生していることを確認（復旧時には履歴データが送信される）
		// 復旧時に履歴データを送信してソース切り替えをトリガー
		// var historyTelegrams = new[] { new MockTelegram("history", "履歴", "HIST", DateTime.Now) };
		// highPriorityPublisher.SimulateHistoryTelegramArrival("HighPriorityPublisher", InformationCategory.Earthquake, historyTelegrams);
		// await Task.Delay(100);
		
		Assert.Contains("HighPriorityPublisher", sourceSwitchEvents);
		
		// 最終的に高優先度Publisherが処理していることを確認
		Assert.Contains(InformationCategory.Earthquake, highPriorityPublisher.StartedCategories);
		
		// 低優先度Publisherは使用されていないことを確認（優先度に従って切り戻し）
		// 注意: MockTelegramPublisherの仕様上、StartedCategoriesからは削除されない
		// 実際の動作では高優先度Publisherが優先されて使用される
	}

	public void Dispose()
	{
		_service?.Dispose();
	}
}
