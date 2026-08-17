using KyoshinEewViewer.Services;
using KyoshinEewViewer.Services.TelegramPublishers;
using KyoshinEewViewer.Tests.Services.Mocks;
using Moq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KyoshinEewViewer.Tests.Services;

/// <summary>
/// TelegramProvideServiceの履歴Telegram配信テスト
/// </summary>
public class TelegramProvideServiceHistoryTests : IDisposable
{
	private readonly Mock<ILogger<TelegramProvideService>> _mockLogger;
	private readonly Mock<IServiceProvider> _mockServiceProvider;
	private readonly TelegramProvideService _service;

public TelegramProvideServiceHistoryTests()
	{
		_mockLogger = new Mock<ILogger<TelegramProvideService>>();
		_mockServiceProvider = new Mock<IServiceProvider>();

		_service = new TelegramProvideService(_mockLogger.Object, _mockServiceProvider.Object);
	}

	[Fact(DisplayName = "履歴電文の到着時にソース切替イベントがトリガーされる")]
	public async Task HistoryTelegramArrival_ShouldTriggerSourceSwitched()
	{
		// Arrange
		var publisher = new MockTelegramPublisher
		{
			Name = "HistoryPublisher",
			SupportedCategories = [InformationCategory.Earthquake]
		};

		var customTypes = new[] { typeof(MockTelegramPublisher) };

		_mockServiceProvider.Setup(x => x.GetService(typeof(MockTelegramPublisher)))
			.Returns(publisher);

		var sourceSwitchedCalled = false;
		string? switchedSourceName = null;
		Telegram[]? receivedTelegrams = null;

		_service.Subscribe(
			InformationCategory.Earthquake,
			sourceSwitched: (name, telegrams) =>
			{
				sourceSwitchedCalled = true;
				switchedSourceName = name;
				receivedTelegrams = telegrams;
				return Task.CompletedTask;
			},
			arrived: _ => Task.CompletedTask,
			failed: _ => { }
		);

		await _service.StartAsync(customTypes);

		// Act
		var historyTelegrams = new[]
		{
			new MockTelegram("hist1", "履歴1", "H001", DateTime.Now.AddHours(-2)),
			new MockTelegram("hist2", "履歴2", "H002", DateTime.Now.AddHours(-1)),
			new MockTelegram("hist3", "履歴3", "H003", DateTime.Now)
		};

		publisher.SimulateHistoryTelegramArrival("HistoryPublisher", InformationCategory.Earthquake, historyTelegrams);
		await Task.Delay(100);

		// Assert
		Assert.True(sourceSwitchedCalled, "SourceSwitchedが呼ばれること");
		Assert.Equal("HistoryPublisher", switchedSourceName);
		Assert.NotNull(receivedTelegrams);
		Assert.Equal(3, receivedTelegrams.Length);
		Assert.Contains(receivedTelegrams, t => t.Key == "hist1");
		Assert.Contains(receivedTelegrams, t => t.Key == "hist2");
		Assert.Contains(receivedTelegrams, t => t.Key == "hist3");
	}

	[Fact(DisplayName = "履歴電文の到着時に複数のサブスクライバー全てに通知する")]
	public async Task HistoryTelegramArrival_MultipleSubscribers_ShouldNotifyAll()
	{
		// Arrange
		var publisher = new MockTelegramPublisher
		{
			Name = "MultiHistoryPublisher",
			SupportedCategories = [InformationCategory.Earthquake]
		};

		var customTypes = new[] { typeof(MockTelegramPublisher) };

		_mockServiceProvider.Setup(x => x.GetService(typeof(MockTelegramPublisher)))
			.Returns(publisher);

		var subscriber1Called = false;
		var subscriber2Called = false;
		Telegram[]? subscriber1Telegrams = null;
		Telegram[]? subscriber2Telegrams = null;

		// 複数のサブスクライバーを登録
		_service.Subscribe(
			InformationCategory.Earthquake,
			sourceSwitched: (name, telegrams) =>
			{
				subscriber1Called = true;
				subscriber1Telegrams = telegrams;
				return Task.CompletedTask;
			},
			arrived: _ => Task.CompletedTask,
			failed: _ => { }
		);

		_service.Subscribe(
			InformationCategory.Earthquake,
			sourceSwitched: (name, telegrams) =>
			{
				subscriber2Called = true;
				subscriber2Telegrams = telegrams;
				return Task.CompletedTask;
			},
			arrived: _ => Task.CompletedTask,
			failed: _ => { }
		);

		await _service.StartAsync(customTypes);

		// Act
		var historyTelegrams = new[]
		{
			new MockTelegram("multi1", "マルチ1", "M001", DateTime.Now)
		};

		publisher.SimulateHistoryTelegramArrival("MultiHistoryPublisher", InformationCategory.Earthquake, historyTelegrams);
		await Task.Delay(100);

		// Assert
		Assert.True(subscriber1Called && subscriber2Called, "すべてのサブスクライバーが通知されること");
		Assert.NotNull(subscriber1Telegrams);
		Assert.NotNull(subscriber2Telegrams);
		Assert.Single(subscriber1Telegrams);
		Assert.Single(subscriber2Telegrams);
		Assert.Equal("multi1", subscriber1Telegrams[0].Key);
		Assert.Equal("multi1", subscriber2Telegrams[0].Key);
	}

	[Fact(DisplayName = "違うカテゴリの履歴電文ではトリガーされない")]
	public async Task HistoryTelegramArrival_WrongCategory_ShouldNotTrigger()
	{
		// Arrange
		var publisher = new MockTelegramPublisher
		{
			Name = "WrongCategoryPublisher",
			SupportedCategories = [InformationCategory.Earthquake, InformationCategory.Tsunami]
		};

		var customTypes = new[] { typeof(MockTelegramPublisher) };

		_mockServiceProvider.Setup(x => x.GetService(typeof(MockTelegramPublisher)))
			.Returns(publisher);

		var earthquakeSourceSwitched = false;
		var tsunamiSourceSwitched = false;

		_service.Subscribe(
			InformationCategory.Earthquake,
			sourceSwitched: (_, _) =>
			{
				earthquakeSourceSwitched = true;
				return Task.CompletedTask;
			},
			arrived: _ => Task.CompletedTask,
			failed: _ => { }
		);

		_service.Subscribe(
			InformationCategory.Tsunami,
			sourceSwitched: (_, _) =>
			{
				tsunamiSourceSwitched = true;
				return Task.CompletedTask;
			},
			arrived: _ => Task.CompletedTask,
			failed: _ => { }
		);

		await _service.StartAsync(customTypes);

		// Act - 地震履歴のみ送信
		var historyTelegrams = new[]
		{
			new MockTelegram("eq1", "地震1", "EQ001", DateTime.Now)
		};

		publisher.SimulateHistoryTelegramArrival("WrongCategoryPublisher", InformationCategory.Earthquake, historyTelegrams);
		await Task.Delay(100);

		// Assert
		Assert.True(earthquakeSourceSwitched, "地震サブスクライバーのみ通知されること");
		Assert.False(tsunamiSourceSwitched, "津波サブスクライバーは通知されないこと");
	}

	[Fact(DisplayName = "空の履歴電文配列でもトリガーされる")]
	public async Task HistoryTelegramArrival_EmptyArray_ShouldStillTrigger()
	{
		// Arrange
		var publisher = new MockTelegramPublisher
		{
			Name = "EmptyHistoryPublisher",
			SupportedCategories = [InformationCategory.Earthquake]
		};

		var customTypes = new[] { typeof(MockTelegramPublisher) };

		_mockServiceProvider.Setup(x => x.GetService(typeof(MockTelegramPublisher)))
			.Returns(publisher);

		var sourceSwitchedCalled = false;
		Telegram[]? receivedTelegrams = null;

		_service.Subscribe(
			InformationCategory.Earthquake,
			sourceSwitched: (name, telegrams) =>
			{
				sourceSwitchedCalled = true;
				receivedTelegrams = telegrams;
				return Task.CompletedTask;
			},
			arrived: _ => Task.CompletedTask,
			failed: _ => { }
		);

		await _service.StartAsync(customTypes);

		// Act - 空の履歴配列を送信
		var emptyTelegrams = Array.Empty<Telegram>();
		publisher.SimulateHistoryTelegramArrival("EmptyHistoryPublisher", InformationCategory.Earthquake, emptyTelegrams);
		await Task.Delay(100);

		// Assert
		Assert.True(sourceSwitchedCalled, "空の配列でもSourceSwitchedが呼ばれること");
		Assert.NotNull(receivedTelegrams);
		Assert.Empty(receivedTelegrams);
	}

	[Fact(DisplayName = "プロバイダ切替後の履歴電文では新しいプロバイダを使用する")]
	public async Task HistoryTelegramArrival_AfterPublisherSwitch_ShouldUseNewPublisher()
	{
		// Arrange
		var primary = new MockTelegramPublisher
		{
			Name = "PrimaryHistory",
			SupportedCategories = [InformationCategory.Earthquake]
		};
		var secondary = new MockTelegramPublisher
		{
			Name = "SecondaryHistory",
			SupportedCategories = [InformationCategory.Earthquake]
		};

		var customTypes = new[]
		{
			typeof(MockTelegramPublisher),
			typeof(MockTelegramPublisher)
		};

		_mockServiceProvider.SetupSequence(x => x.GetService(typeof(MockTelegramPublisher)))
			.Returns(primary)
			.Returns(secondary);

		var sourceSwitchHistory = new List<string>();

		_service.Subscribe(
			InformationCategory.Earthquake,
			sourceSwitched: (name, telegrams) =>
			{
				sourceSwitchHistory.Add(name);
				return Task.CompletedTask;
			},
			arrived: _ => Task.CompletedTask,
			failed: _ => { }
		);

		await _service.StartAsync(customTypes);

		// Act
		// Primaryから履歴送信
		var primaryHistory = new[]
		{
			new MockTelegram("p1", "Primary履歴", "P001", DateTime.Now)
		};
		primary.SimulateHistoryTelegramArrival("PrimaryHistory", InformationCategory.Earthquake, primaryHistory);
		await Task.Delay(100);

		// Primaryが失敗してSecondaryに切り替え
		primary.SimulateFailure([InformationCategory.Earthquake], false);
		await Task.Delay(100);

		// Secondaryから履歴送信
		var secondaryHistory = new[]
		{
			new MockTelegram("s1", "Secondary履歴", "S001", DateTime.Now)
		};
		secondary.SimulateHistoryTelegramArrival("SecondaryHistory", InformationCategory.Earthquake, secondaryHistory);
		await Task.Delay(100);

		// Assert
		Assert.Equal(2, sourceSwitchHistory.Count);
		Assert.Equal("PrimaryHistory", sourceSwitchHistory[0]);
		Assert.Equal("SecondaryHistory", sourceSwitchHistory[1]);
	}

	[Fact(DisplayName = "非アクティブなプロバイダからの履歴電文ではトリガーされない")]
	public async Task HistoryTelegramArrival_FromNonActivePublisher_ShouldNotTrigger()
	{
		// Arrange
		var activePublisher = new MockTelegramPublisher
		{
			Name = "ActivePublisher",
			SupportedCategories = [InformationCategory.Earthquake]
		};
		var inactivePublisher = new MockTelegramPublisher
		{
			Name = "InactivePublisher",
			SupportedCategories = [] // 地震をサポートしない
		};

		var customTypes = new[]
		{
			typeof(MockTelegramPublisher),
			typeof(MockTelegramPublisher)
		};

		_mockServiceProvider.SetupSequence(x => x.GetService(typeof(MockTelegramPublisher)))
			.Returns(activePublisher)
			.Returns(inactivePublisher);

		var sourceSwitchedCount = 0;

		_service.Subscribe(
			InformationCategory.Earthquake,
			sourceSwitched: (name, telegrams) =>
			{
				sourceSwitchedCount++;
				return Task.CompletedTask;
			},
			arrived: _ => Task.CompletedTask,
			failed: _ => { }
		);

		await _service.StartAsync(customTypes);

		// Act
		// アクティブでないPublisherから履歴送信を試行
		var inactiveHistory = new[]
		{
			new MockTelegram("inactive", "非アクティブ履歴", "I001", DateTime.Now)
		};
		inactivePublisher.SimulateHistoryTelegramArrival("InactivePublisher", InformationCategory.Earthquake, inactiveHistory);
		await Task.Delay(100);

		// アクティブなPublisherから履歴送信
		var activeHistory = new[]
		{
			new MockTelegram("active", "アクティブ履歴", "A001", DateTime.Now)
		};
		activePublisher.SimulateHistoryTelegramArrival("ActivePublisher", InformationCategory.Earthquake, activeHistory);
		await Task.Delay(100);

		// Assert
		// アクティブなPublisherからの履歴のみが処理される
		Assert.Equal(1, sourceSwitchedCount);
	}

	[Fact(DisplayName = "大量の履歴電文データを効率的に処理する")]
	public async Task HistoryTelegramArrival_LargeDataset_ShouldHandleEfficiently()
	{
		// Arrange
		var publisher = new MockTelegramPublisher
		{
			Name = "LargeHistoryPublisher",
			SupportedCategories = [InformationCategory.Earthquake]
		};

		var customTypes = new[] { typeof(MockTelegramPublisher) };

		_mockServiceProvider.Setup(x => x.GetService(typeof(MockTelegramPublisher)))
			.Returns(publisher);

		var sourceSwitchedCalled = false;
		Telegram[]? receivedTelegrams = null;

		_service.Subscribe(
			InformationCategory.Earthquake,
			sourceSwitched: (name, telegrams) =>
			{
				sourceSwitchedCalled = true;
				receivedTelegrams = telegrams;
				return Task.CompletedTask;
			},
			arrived: _ => Task.CompletedTask,
			failed: _ => { }
		);

		await _service.StartAsync(customTypes);

		// Act - 大量の履歴データを送信
		var largeHistory = Enumerable.Range(1, 1000)
			.Select(i => new MockTelegram($"large{i}", $"大量履歴{i}", $"L{i:000}", DateTime.Now.AddMinutes(-i)))
			.Cast<Telegram>()
			.ToArray();

		var startTime = DateTime.Now;
		publisher.SimulateHistoryTelegramArrival("LargeHistoryPublisher", InformationCategory.Earthquake, largeHistory);
		await Task.Delay(500);
		var endTime = DateTime.Now;

		// Assert
		Assert.True(sourceSwitchedCalled, "大量データでもSourceSwitchedが呼ばれること");
		Assert.NotNull(receivedTelegrams);
		Assert.Equal(1000, receivedTelegrams.Length);
		
		// パフォーマンス確認（1秒以内で処理完了）
		var processingTime = endTime - startTime;
		Assert.True(processingTime.TotalSeconds < 1.0, $"処理時間が1秒を超過: {processingTime.TotalSeconds}秒");
	}

	public void Dispose()
	{
		_service?.Dispose();
	}
}
