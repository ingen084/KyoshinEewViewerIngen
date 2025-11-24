using KyoshinEewViewer.Services;
using KyoshinEewViewer.Services.TelegramPublishers;
using KyoshinEewViewer.Tests.Services.Mocks;
using Moq;
using Splat;

namespace KyoshinEewViewer.Tests.Services;

/// <summary>
/// TelegramProvideServiceのエッジケーステスト
/// </summary>
public class TelegramProvideServiceEdgeCaseTests : IDisposable
{
	private readonly Mock<ILogManager> _mockLogManager;
	private readonly Mock<IFullLogger> _mockLogger;
	private readonly Mock<IReadonlyDependencyResolver> _mockServiceProvider;
	private readonly TelegramProvideService _service;

public TelegramProvideServiceEdgeCaseTests()
	{
		_mockLogManager = new Mock<ILogManager>();
		_mockLogger = new Mock<IFullLogger>();
		
		// GetLoggerメソッドを適切にセットアップするために、実際の実装を提供
		_mockLogManager.Setup(x => x.GetLogger(It.IsAny<Type>()))
			.Returns(_mockLogger.Object);

		_mockServiceProvider = new Mock<IReadonlyDependencyResolver>();

		_service = new TelegramProvideService(_mockLogManager.Object, _mockServiceProvider.Object);
	}

	[Fact(DisplayName = "単一プロバイダが全カテゴリをサポートする場合、全てのタイプを処理する")]
	public async Task SinglePublisher_AllCategories_ShouldHandleAllTypes()
	{
		// Arrange
		var universalPublisher = new MockTelegramPublisher
		{
			Name = "UniversalPublisher",
			SupportedCategories = Enum.GetValues<InformationCategory>().ToList()
		};

		var customTypes = new[] { typeof(MockTelegramPublisher) };

		_mockServiceProvider.Setup(x => x.GetService(typeof(MockTelegramPublisher), null))
			.Returns(universalPublisher);

		// すべてのカテゴリをサブスクライブ
		foreach (var category in Enum.GetValues<InformationCategory>())
		{
			_service.Subscribe(
				category,
				(_, _) => Task.CompletedTask,
				_ => Task.CompletedTask,
				_ => { }
			);
		}

		// Act
		await _service.StartAsync(customTypes);

		// Assert
		var allCategories = Enum.GetValues<InformationCategory>().ToList();
		foreach (var category in allCategories)
		{
			Assert.Contains(category, universalPublisher.StartedCategories);
		}
		
		// 重複がないことを確認
		var distinctCategories = universalPublisher.StartedCategories.Distinct().Count();
		Assert.Equal(allCategories.Count, distinctCategories);
	}

	[Fact(DisplayName = "サブスクライバーがいない場合でもエラーなく開始する")]
	public async Task NoSubscribers_ShouldStartWithoutError()
	{
		// Arrange
		var publisher = new MockTelegramPublisher
		{
			Name = "NoSubscriberPublisher",
			SupportedCategories = [InformationCategory.Earthquake]
		};

		var customTypes = new[] { typeof(MockTelegramPublisher) };

		_mockServiceProvider.Setup(x => x.GetService(typeof(MockTelegramPublisher), null))
			.Returns(publisher);

		// Act - サブスクライバーなしでStart
		await _service.StartAsync(customTypes);

		// Assert
		// Started プロパティはプライベートなので、例外が発生しないことで確認
		Assert.True(publisher.IsInitialized);
		Assert.Empty(publisher.StartedCategories); // サブスクライバーがないので何も開始されない
	}

	[Fact(DisplayName = "重複したサブスクリプションを適切に処理する")]
	public async Task DuplicateSubscription_ShouldHandleGracefully()
	{
		// Arrange
		var publisher = new MockTelegramPublisher
		{
			Name = "DuplicatePublisher",
			SupportedCategories = [InformationCategory.Earthquake]
		};

		var customTypes = new[] { typeof(MockTelegramPublisher) };

		_mockServiceProvider.Setup(x => x.GetService(typeof(MockTelegramPublisher), null))
			.Returns(publisher);

		var callCount = 0;

		// 同じカテゴリに複数回サブスクライブ
		for (int i = 0; i < 3; i++)
		{
			_service.Subscribe(
				InformationCategory.Earthquake,
				(_, _) => Task.CompletedTask,
				_ =>
				{
					callCount++;
					return Task.CompletedTask;
				},
				_ => { }
			);
		}

		await _service.StartAsync(customTypes);

		// Act
		var telegram = new MockTelegram("dup", "重複テスト", "DUP", DateTime.Now);
		publisher.SimulateTelegramArrival(InformationCategory.Earthquake, telegram);
		await Task.Delay(100);

		// Assert
		// すべてのサブスクライバーが呼ばれること
		Assert.Equal(3, callCount);
		
		// Publisherは一度だけ開始されること
		Assert.Equal(1, publisher.StartedCategories.Count(c => c == InformationCategory.Earthquake));
	}

	[Fact(DisplayName = "カテゴリゼロのプロバイダはスキップされる")]
	public async Task PublisherWithZeroCategories_ShouldBeSkipped()
	{
		// Arrange
		var emptyPublisher = new MockTelegramPublisher
		{
			Name = "EmptyPublisher",
			SupportedCategories = [] // 何もサポートしない
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
			.Returns(emptyPublisher)
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
		Assert.True(emptyPublisher.IsInitialized);
		Assert.True(workingPublisher.IsInitialized);
		Assert.Empty(emptyPublisher.StartedCategories);
		Assert.Contains(InformationCategory.Earthquake, workingPublisher.StartedCategories);
	}

	[Fact(DisplayName = "非常に長いプロバイダ名を正しく処理する")]
	public async Task VeryLongPublisherName_ShouldHandleCorrectly()
	{
		// Arrange
		var longNamePublisher = new MockTelegramPublisher
		{
			Name = new string('A', 1000), // 非常に長い名前
			SupportedCategories = [InformationCategory.Earthquake]
		};

		var customTypes = new[] { typeof(MockTelegramPublisher) };

		_mockServiceProvider.Setup(x => x.GetService(typeof(MockTelegramPublisher), null))
			.Returns(longNamePublisher);

		string? receivedSourceName = null;

		_service.Subscribe(
			InformationCategory.Earthquake,
			sourceSwitched: (name, telegrams) =>
			{
				receivedSourceName = name;
				return Task.CompletedTask;
			},
			arrived: _ => Task.CompletedTask,
			failed: _ => { }
		);

		await _service.StartAsync(customTypes);

		// Act
		var historyTelegrams = new[]
		{
			new MockTelegram("long", "ロング", "LONG", DateTime.Now)
		};
		longNamePublisher.SimulateHistoryTelegramArrival(longNamePublisher.Name, InformationCategory.Earthquake, historyTelegrams);
		await Task.Delay(100);

		// Assert
		Assert.NotNull(receivedSourceName);
		Assert.Equal(longNamePublisher.Name, receivedSourceName);
	}

	[Fact(DisplayName = "極端な日時を正しく処理する")]
	public async Task ExtremeDateTimes_ShouldHandleCorrectly()
	{
		// Arrange
		var publisher = new MockTelegramPublisher
		{
			Name = "ExtremeTimePublisher",
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

		// Act - 極端な日時のTelegramを送信
		var extremeTelegrams = new[]
		{
			new MockTelegram("min", "最小時刻", "MIN", DateTime.MinValue),
			new MockTelegram("max", "最大時刻", "MAX", DateTime.MaxValue),
			new MockTelegram("unix", "Unix開始", "UNIX", new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc))
		};

		foreach (var telegram in extremeTelegrams)
		{
			publisher.SimulateTelegramArrival(InformationCategory.Earthquake, telegram);
		}
		
		await Task.Delay(200);

		// Assert
		Assert.Equal(3, receivedTelegrams.Count);
		Assert.Contains(receivedTelegrams, t => t.ArrivalTime == DateTime.MinValue);
		Assert.Contains(receivedTelegrams, t => t.ArrivalTime == DateTime.MaxValue);
		Assert.Contains(receivedTelegrams, t => t.ArrivalTime.Year == 1970);
	}

	[Fact(DisplayName = "高速な開始停止サイクルでも整合性を維持する")]
	public async Task RapidStartStopCycle_ShouldMaintainConsistency()
	{
		// Arrange
		var publisher = new MockTelegramPublisher
		{
			Name = "CyclePublisher",
			SupportedCategories = [InformationCategory.Earthquake, InformationCategory.Tsunami]
		};

		var customTypes = new[] { typeof(MockTelegramPublisher) };

		_mockServiceProvider.Setup(x => x.GetService(typeof(MockTelegramPublisher), null))
			.Returns(publisher);

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

		// Act - 高速でカテゴリサポートを変更
		for (int i = 0; i < 10; i++)
		{
			if (i % 2 == 0)
			{
				publisher.SupportedCategories = [InformationCategory.Earthquake];
			}
			else
			{
				publisher.SupportedCategories = [InformationCategory.Tsunami];
			}
			
			publisher.SimulateRecovery();
			await Task.Delay(10);
		}

		await Task.Delay(200);

		// Assert
		// データの整合性を確認
		var startedCategories = publisher.StartedCategories;
		
		// 重複がないことを確認
		var duplicates = startedCategories
			.GroupBy(x => x)
			.Where(g => g.Count() > 1)
			.Select(g => g.Key);
		Assert.Empty(duplicates);
		
		// 何らかのカテゴリが処理されていること
		Assert.NotEmpty(startedCategories);
	}

	[Fact(DisplayName = "ゼロ遅延の操作を正しく処理する")]
	public async Task ZeroDelayOperations_ShouldHandleCorrectly()
	{
		// Arrange
		var publisher = new MockTelegramPublisher
		{
			Name = "ZeroDelayPublisher",
			SupportedCategories = [InformationCategory.Earthquake]
		};

		var customTypes = new[] { typeof(MockTelegramPublisher) };

		_mockServiceProvider.Setup(x => x.GetService(typeof(MockTelegramPublisher), null))
			.Returns(publisher);

		var receivedCount = 0;

		_service.Subscribe(
			InformationCategory.Earthquake,
			(_, _) => Task.CompletedTask,
			_ =>
			{
				receivedCount++;
				return Task.CompletedTask;
			},
			_ => { }
		);

		await _service.StartAsync(customTypes);

		// Act - 遅延なしで連続してTelegramを送信
		for (int i = 0; i < 100; i++)
		{
			var telegram = new MockTelegram($"fast{i}", $"高速{i}", $"F{i:000}", DateTime.Now);
			publisher.SimulateTelegramArrival(InformationCategory.Earthquake, telegram);
		}

		await Task.Delay(500);

		// Assert
		Assert.Equal(100, receivedCount);
	}

	[Fact(DisplayName = "プロバイダ優先度の境界でも順序を尊重する")]
	public async Task PublisherPriorityBoundary_ShouldRespectOrdering()
	{
		// Arrange - 大量のPublisherで優先度境界をテスト
		var publishers = Enumerable.Range(1, 50)
			.Select(i => new MockTelegramPublisher
			{
				Name = $"Publisher{i:00}",
				SupportedCategories = [InformationCategory.Earthquake]
			})
			.ToArray();

		var customTypes = Enumerable.Repeat(typeof(MockTelegramPublisher), 50).ToArray();

		_mockServiceProvider.Setup(x => x.GetService(typeof(MockTelegramPublisher), null))
			.Returns(() =>
			{
				var callCount = _mockServiceProvider.Invocations.Count(i => i.Method.Name == nameof(IReadonlyDependencyResolver.GetService));
				return callCount <= publishers.Length ? publishers[callCount - 1] : null;
			});

		_service.Subscribe(
			InformationCategory.Earthquake,
			(_, _) => Task.CompletedTask,
			_ => Task.CompletedTask,
			_ => { }
		);

		// Act
		await _service.StartAsync(customTypes);

		// Assert
		// 最高優先度（最初の）Publisherのみが処理していること
		Assert.Contains(InformationCategory.Earthquake, publishers[0].StartedCategories);
		
		// 他のPublisherは処理していないこと
		for (int i = 1; i < publishers.Length; i++)
		{
			Assert.DoesNotContain(InformationCategory.Earthquake, publishers[i].StartedCategories);
		}
	}



	public void Dispose()
	{
		_service?.Dispose();
		GC.SuppressFinalize(this);
	}
}
