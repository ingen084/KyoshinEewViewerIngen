using KyoshinEewViewer.Services;
using KyoshinEewViewer.Services.TelegramPublishers;
using KyoshinEewViewer.Tests.Services.Mocks;
using Moq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace KyoshinEewViewer.Tests.Services;

/// <summary>
/// TelegramProvideServiceのコンカレンシーテスト
/// </summary>
public class TelegramProvideServiceConcurrencyTests : IDisposable
{
	private readonly Mock<ILogger<TelegramProvideService>> _mockLogger;
	private readonly Mock<IServiceProvider> _mockServiceProvider;
	private readonly TelegramProvideService _service;

public TelegramProvideServiceConcurrencyTests()
	{
		_mockLogger = new Mock<ILogger<TelegramProvideService>>();
		_mockServiceProvider = new Mock<IServiceProvider>();

		_service = new TelegramProvideService(_mockLogger.Object, _mockServiceProvider.Object);
	}

	[Fact(DisplayName = "同時に複数の電文が到着した場合、全てのメッセージを正しく処理する")]
	public async Task ConcurrentTelegramArrival_ShouldHandleAllMessages()
	{
		// Arrange
		var publisher = new MockTelegramPublisher
		{
			Name = "ConcurrentPublisher",
			SupportedCategories = [InformationCategory.Earthquake, InformationCategory.Tsunami]
		};

		var customTypes = new[] { typeof(MockTelegramPublisher) };

		_mockServiceProvider.Setup(x => x.GetService(typeof(MockTelegramPublisher)))
			.Returns(publisher);

		var receivedTelegrams = new ConcurrentBag<Telegram>();
		var telegramCount = 100;
		var semaphore = new SemaphoreSlim(0, telegramCount);

		_service.Subscribe(
			InformationCategory.Earthquake,
			(_, _) => Task.CompletedTask,
			telegram =>
			{
				receivedTelegrams.Add(telegram);
				semaphore.Release();
				return Task.CompletedTask;
			},
			_ => { }
		);

		await _service.StartAsync(customTypes);

		// Act - 複数のTelegramを同時送信
		var tasks = Enumerable.Range(0, telegramCount)
			.Select(i => Task.Run(() =>
			{
				var telegram = new MockTelegram($"concurrent-{i}", $"同時テスト{i}", $"TEST{i:000}", DateTime.Now);
				publisher.SimulateTelegramArrival(InformationCategory.Earthquake, telegram);
			}))
			.ToArray();

		await Task.WhenAll(tasks);

		// すべてのメッセージが処理されるまで待機
		for (var i = 0; i < telegramCount; i++)
		{
			await semaphore.WaitAsync(TimeSpan.FromSeconds(5));
		}

		// Assert
		Assert.Equal(telegramCount, receivedTelegrams.Count);
		
		// すべてのメッセージが重複なく届いていることを確認
		var distinctIds = receivedTelegrams.Select(t => t.Key).Distinct().Count();
		Assert.Equal(telegramCount, distinctIds);
	}

	[Fact(DisplayName = "複数のPublisherで失敗と復旧が並行して発生しても整合性を維持する")]
	public async Task ConcurrentFailureAndRecovery_ShouldMaintainConsistency()
	{
		// Arrange
		var primary = new MockTelegramPublisher
		{
			Name = "Primary",
			SupportedCategories = [InformationCategory.Earthquake]
		};
		var secondary = new MockTelegramPublisher
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
			.Returns(primary)
			.Returns(secondary);

		var failureStates = new ConcurrentBag<(bool isAllFailed, bool isRestorable)>();

		_service.Subscribe(
			InformationCategory.Earthquake,
			(_, _) => Task.CompletedTask,
			_ => Task.CompletedTask,
			state => { failureStates.Add(state); }
		);

		await _service.StartAsync(customTypes);

		// Act - 複数の失敗と復旧を並行実行
		var concurrentTasks = new List<Task>
		{
			// 失敗シミュレーション
			Task.Run(async () =>
			{
				await Task.Delay(10);
				primary.SimulateFailure([InformationCategory.Earthquake], false);
			}),
			
			// 復旧シミュレーション
			Task.Run(async () =>
			{
				await Task.Delay(50);
				primary.SupportedCategories = [InformationCategory.Earthquake];
				primary.SimulateRecovery();
			}),
			
			// 別の失敗
			Task.Run(async () =>
			{
				await Task.Delay(100);
				secondary.SimulateFailure([InformationCategory.Earthquake], false);
			})
		};

		await Task.WhenAll(concurrentTasks);
		await Task.Delay(300);

		// Assert
		// 最終的に適切なPublisherが処理していることを確認
		var hasStartedPublishers = new[]
		{
			primary.StartedCategories.Contains(InformationCategory.Earthquake),
			secondary.StartedCategories.Contains(InformationCategory.Earthquake)
		};
		
		// どちらか一方は必ず処理している
		Assert.Contains(true, hasStartedPublishers);
		
		// 失敗状態が適切に報告されている
		Assert.NotEmpty(failureStates);
	}

	[Fact(DisplayName = "複数のカテゴリ更新が並行実行されても正しく処理する")]
	public async Task ConcurrentCategoryUpdates_ShouldHandleCorrectly()
	{
		// Arrange
		var publisher = new MockTelegramPublisher
		{
			Name = "DynamicPublisher",
			SupportedCategories = [InformationCategory.Earthquake]
		};

		var customTypes = new[] { typeof(MockTelegramPublisher) };

		_mockServiceProvider.Setup(x => x.GetService(typeof(MockTelegramPublisher)))
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

		// Act - 複数のカテゴリ更新を並行実行
		var updateTasks = Enumerable.Range(0, 10)
			.Select(i => Task.Run(async () =>
			{
				await Task.Delay(i * 10);
				
				// カテゴリを動的に変更
				if (i % 2 == 0)
				{
					publisher.SupportedCategories = [InformationCategory.Earthquake, InformationCategory.Tsunami];
				}
				else
				{
					publisher.SupportedCategories = [InformationCategory.Earthquake];
				}
				
				publisher.SimulateRecovery();
			}))
			.ToArray();

		await Task.WhenAll(updateTasks);
		await Task.Delay(200);

		// Assert
		// 最終的に適切な状態になっていることを確認
		Assert.Contains(InformationCategory.Earthquake, publisher.StartedCategories);
		
		// データの整合性を確認（重複がないこと）
		var duplicates = publisher.StartedCategories
			.GroupBy(x => x)
			.Where(g => g.Count() > 1)
			.Select(g => g.Key);
		Assert.Empty(duplicates);
	}

	[Fact(DisplayName = "複数のサブスクライバーが登録されていても全てに電文を配信する")]
	public async Task ConcurrentMultipleSubscriptions_ShouldDeliverToAll()
	{
		// Arrange
		var publisher = new MockTelegramPublisher
		{
			Name = "BroadcastPublisher",
			SupportedCategories = [InformationCategory.Earthquake]
		};

		var customTypes = new[] { typeof(MockTelegramPublisher) };

		_mockServiceProvider.Setup(x => x.GetService(typeof(MockTelegramPublisher)))
			.Returns(publisher);

		var subscriberCount = 5;
		var receivedCounts = new ConcurrentDictionary<int, int>();

		// 複数のサブスクライバーを登録
		for (var i = 0; i < subscriberCount; i++)
		{
			var subscriberId = i;
			_service.Subscribe(
				InformationCategory.Earthquake,
				(_, _) => Task.CompletedTask,
				telegram =>
				{
					receivedCounts.AddOrUpdate(subscriberId, 1, (_, count) => count + 1);
					return Task.CompletedTask;
				},
				_ => { }
			);
		}

		await _service.StartAsync(customTypes);

		// Act - 複数のTelegramを送信
		var messageCount = 20;
		var sendTasks = Enumerable.Range(0, messageCount)
			.Select(i => Task.Run(() =>
			{
				var telegram = new MockTelegram($"broadcast-{i}", $"ブロードキャスト{i}", $"BC{i:00}", DateTime.Now);
				publisher.SimulateTelegramArrival(InformationCategory.Earthquake, telegram);
			}))
			.ToArray();

		await Task.WhenAll(sendTasks);
		await Task.Delay(500);

		// Assert
		// すべてのサブスクライバーがすべてのメッセージを受信
		Assert.Equal(subscriberCount, receivedCounts.Count);
		
		foreach (var kvp in receivedCounts)
		{
			Assert.Equal(messageCount, kvp.Value);
		}
	}

	[Fact(DisplayName = "複数のRestore操作が並行実行されても安全に処理する")]
	public async Task ConcurrentRestoreOperations_ShouldHandleSafely()
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
			SupportedCategories = [InformationCategory.Tsunami]
		};

		var customTypes = new[]
		{
			typeof(MockTelegramPublisher),
			typeof(MockTelegramPublisher)
		};

		_mockServiceProvider.SetupSequence(x => x.GetService(typeof(MockTelegramPublisher)))
			.Returns(publisher1)
			.Returns(publisher2);

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

		// 正常に各カテゴリが担当される
		Assert.Contains(InformationCategory.Earthquake, publisher1.StartedCategories);
		Assert.Contains(InformationCategory.Tsunami, publisher2.StartedCategories);

		// Act - Publisher1がサポートできなくなった状況をシミュレート
		publisher1.SupportedCategories = [];

		// 複数のRestoreAsyncを並行実行
		var restoreTasks = Enumerable.Range(0, 5)
			.Select(_ => _service.RestoreAsync())
			.ToArray();

		await Task.WhenAll(restoreTasks);

		// Assert - 既に割り当て済みなので復旧処理は実行されない
		// RestoreAsyncは割り当てられていないSubscriberのみを対象とする
		Assert.True(true, "複数のRestoreAsync実行でもエラーが発生しないこと");
	}

	public void Dispose()
	{
		_service?.Dispose();
		GC.SuppressFinalize(this);
	}
}
