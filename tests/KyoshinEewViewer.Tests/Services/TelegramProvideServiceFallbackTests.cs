using KyoshinEewViewer.Services;
using KyoshinEewViewer.Services.TelegramPublishers;
using KyoshinEewViewer.Tests.Services.Mocks;
using Moq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KyoshinEewViewer.Tests.Services;

/// <summary>
/// TelegramProvideServiceのフォールバック機能テスト
/// </summary>
public class TelegramProvideServiceFallbackTests : IDisposable
{
	private readonly Mock<ILogger<TelegramProvideService>> _mockLogger;
	private readonly Mock<IServiceProvider> _mockServiceProvider;
	private readonly TelegramProvideService _service;

public TelegramProvideServiceFallbackTests()
	{
		_mockLogger = new Mock<ILogger<TelegramProvideService>>();
		_mockServiceProvider = new Mock<IServiceProvider>();

		_service = new TelegramProvideService(_mockLogger.Object, _mockServiceProvider.Object);
	}

	[Fact(DisplayName = "プライマリプロバイダが失敗した場合、セカンダリプロバイダにフォールバックする")]
	public async Task PrimaryPublisherFailure_ShouldFallbackToSecondary()
	{
		// Arrange
		var primaryPublisher = new MockTelegramPublisher
		{
			Name = "Primary",
			SupportedCategories = [InformationCategory.Earthquake, InformationCategory.Tsunami]
		};
		var secondaryPublisher = new MockTelegramPublisher
		{
			Name = "Secondary",
			SupportedCategories = [InformationCategory.Earthquake, InformationCategory.EewForecast]
		};

		var customTypes = new[]
		{
			typeof(MockTelegramPublisher), // Primary
			typeof(MockTelegramPublisher)  // Secondary
		};

		_mockServiceProvider.SetupSequence(x => x.GetService(typeof(MockTelegramPublisher)))
			.Returns(primaryPublisher)
			.Returns(secondaryPublisher);

		// サブスクライバーの登録
		var earthquakeReceived = new List<Telegram>();
		var failureState = (false, false);

		_service.Subscribe(
			InformationCategory.Earthquake,
			sourceSwitched: (name, telegrams) => Task.CompletedTask,
			arrived: telegram =>
			{
				earthquakeReceived.Add(telegram);
				return Task.CompletedTask;
			},
			failed: state =>
			{
				failureState = state;
			}
		);

		await _service.StartAsync(customTypes);

		// Act - Primary が失敗（復旧不可能）
		primaryPublisher.SimulateFailure(
			[InformationCategory.Earthquake],
			isRestorable: false
		);

		// 非同期処理の待機
		await Task.Delay(200);

		// Assert
		// フォールバック先のSecondaryPublisherがEarthquakeを開始する
		Assert.Contains(InformationCategory.Earthquake, secondaryPublisher.StartedCategories);
		
		// Primary Publisherは失敗状態だが、StartedCategoriesからは削除されない（テスト用メンバの仕様）
		// 実際の動作では、UsingPublisherでSecondaryに切り替わっている

		// Secondary経由でデータが届くことを確認
		var testTelegram = new MockTelegram("test", "テスト", "TEST", DateTime.Now);
		secondaryPublisher.SimulateTelegramArrival(InformationCategory.Earthquake, testTelegram);

		await Task.Delay(50);
		Assert.Contains(earthquakeReceived, t => t == testTelegram);
	}

	[Fact(DisplayName = "復旧可能な失敗の場合、フォールバックしない")]
	public async Task RestorableFailure_ShouldNotFallback()
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

		// サブスクライバーの登録
		var failureReported = false;
		var failureState = (false, false);

		_service.Subscribe(
			InformationCategory.Earthquake,
			(_, _) => Task.CompletedTask,
			_ => Task.CompletedTask,
			state =>
			{
				failureReported = true;
				failureState = state;
			}
		);

		await _service.StartAsync(customTypes);

		// Act - Primary が復旧可能な失敗
		primaryPublisher.SimulateFailure(
			[InformationCategory.Earthquake],
			isRestorable: true
		);

		await Task.Delay(200);

		// Assert
		Assert.Empty(secondaryPublisher.StartedCategories);
		Assert.True(failureReported);
		Assert.False(failureState.Item1);
		Assert.True(failureState.Item2);
	}

	[Fact(DisplayName = "全てのプロバイダが失敗した場合、完全失敗を報告する")]
	public async Task AllPublishersFailed_ShouldReportCompleteFailure()
	{
		// Arrange
		var primaryPublisher = new MockTelegramPublisher
		{
			Name = "Primary",
			SupportedCategories = [InformationCategory.Earthquake] // Tsunamiサポートなし
		};
		var secondaryPublisher = new MockTelegramPublisher
		{
			Name = "Secondary",
			SupportedCategories = [InformationCategory.EewForecast] // Earthquakeサポートなし
		};

		var failureReported = false;
		var failureState = (false, false);

		_service.Subscribe(
			InformationCategory.Tsunami, // どちらもサポートしていない
			sourceSwitched: (_, _) => Task.CompletedTask,
			arrived: _ => Task.CompletedTask,
			failed: state =>
			{
				failureReported = true;
				failureState = state;
			}
		);

		var customTypes = new[]
		{
			typeof(MockTelegramPublisher),
			typeof(MockTelegramPublisher)
		};

		_mockServiceProvider.SetupSequence(x => x.GetService(typeof(MockTelegramPublisher)))
			.Returns(primaryPublisher)
			.Returns(secondaryPublisher);

		// Act
		await _service.StartAsync(customTypes);

		// Assert
		Assert.True(failureReported, "失敗が報告されること");
		Assert.True(failureState.Item1, "完全失敗として報告されること");
	}

	[Fact(DisplayName = "連続した失敗が発生しても適切に処理する")]
	public async Task CascadingFailures_ShouldHandleGracefully()
	{
		// Arrange
		var publisher1 = new MockTelegramPublisher
		{
			Name = "Publisher1",
			SupportedCategories = [InformationCategory.Earthquake]
		};
		var publisher2 = new MockTelegramPublisher
		{
			Name = "Publisher2",
			SupportedCategories = [InformationCategory.Earthquake]
		};
		var publisher3 = new MockTelegramPublisher
		{
			Name = "Publisher3",
			SupportedCategories = [InformationCategory.Earthquake]
		};

		var customTypes = new[]
		{
			typeof(MockTelegramPublisher),
			typeof(MockTelegramPublisher),
			typeof(MockTelegramPublisher)
		};

		_mockServiceProvider.SetupSequence(x => x.GetService(typeof(MockTelegramPublisher)))
			.Returns(publisher1)
			.Returns(publisher2)
			.Returns(publisher3);

		var failureStates = new List<(bool isAllFailed, bool isRestorable)>();

		_service.Subscribe(
			InformationCategory.Earthquake,
			(_, _) => Task.CompletedTask,
			_ => Task.CompletedTask,
			state => failureStates.Add(state)
		);

		await _service.StartAsync(customTypes);

		// Act & Assert
		// Publisher1が失敗 → Publisher2へ
		publisher1.SimulateFailure([InformationCategory.Earthquake], false);
		await Task.Delay(100);
		Assert.Contains(InformationCategory.Earthquake, publisher2.StartedCategories);
		// Publisher1は失敗したが、StartedCategoriesからは削除されない（テスト用メンバの仕様）

		// Publisher2も失敗 → Publisher3へ
		publisher2.SimulateFailure([InformationCategory.Earthquake], false);
		await Task.Delay(100);
		Assert.Contains(InformationCategory.Earthquake, publisher3.StartedCategories);
		// Publisher2も失敗したが、StartedCategoriesからは削除されない（テスト用メンバの仕様）

		// Publisher3も失敗 → 完全失敗
		publisher3.SimulateFailure([InformationCategory.Earthquake], false);
		await Task.Delay(100);

		// 最終的に完全失敗が報告される
		Assert.Contains((true, false), failureStates);
	}

	[Fact(DisplayName = "部分的な失敗の場合、影響されたカテゴリのみフォールバックする")]
	public async Task PartialFailure_ShouldFallbackOnlyAffectedCategories()
	{
		// Arrange
		var primaryPublisher = new MockTelegramPublisher
		{
			Name = "Primary",
			SupportedCategories = [InformationCategory.Earthquake, InformationCategory.Tsunami]
		};
		var secondaryPublisher = new MockTelegramPublisher
		{
			Name = "Secondary",
			SupportedCategories = [InformationCategory.Earthquake] // Tsunamiはサポートしない
		};

		var customTypes = new[]
		{
			typeof(MockTelegramPublisher),
			typeof(MockTelegramPublisher)
		};

		_mockServiceProvider.SetupSequence(x => x.GetService(typeof(MockTelegramPublisher)))
			.Returns(primaryPublisher)
			.Returns(secondaryPublisher);

		var earthquakeFailures = new List<(bool, bool)>();
		var tsunamiFailures = new List<(bool, bool)>();

		_service.Subscribe(
			InformationCategory.Earthquake,
			(_, _) => Task.CompletedTask,
			_ => Task.CompletedTask,
			state => earthquakeFailures.Add(state)
		);

		_service.Subscribe(
			InformationCategory.Tsunami,
			(_, _) => Task.CompletedTask,
			_ => Task.CompletedTask,
			state => tsunamiFailures.Add(state)
		);

		await _service.StartAsync(customTypes);

		// Act - PrimaryがEarthquakeのみ失敗
		primaryPublisher.SimulateFailure([InformationCategory.Earthquake], false);
		await Task.Delay(200);

		// Assert
		// EarthquakeはSecondaryにフォールバック
		Assert.Contains(InformationCategory.Earthquake, secondaryPublisher.StartedCategories);
		// TsunamiはPrimaryのまま（失敗していない）
		Assert.Contains(InformationCategory.Tsunami, primaryPublisher.StartedCategories);
		Assert.DoesNotContain(InformationCategory.Tsunami, secondaryPublisher.StartedCategories);
	}

	[Fact(DisplayName = "サポートしていないカテゴリの失敗はフォールバックしない")]
	public async Task NonSupportedCategoryFailure_DoesNotFallback()
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

		// Act - Primaryが自分が管理していないカテゴリで失敗を報告
		primaryPublisher.SimulateFailure([InformationCategory.Tsunami], false);
		await Task.Delay(100);

		// Assert
		// SecondaryのTsunamiに影響はない
		Assert.Contains(InformationCategory.Tsunami, secondaryPublisher.StartedCategories);
		Assert.Contains(InformationCategory.Earthquake, primaryPublisher.StartedCategories);
	}

	public void Dispose()
	{
		_service?.Dispose();
	}
}
