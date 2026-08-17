using KyoshinEewViewer.Services;
using KyoshinEewViewer.Services.TelegramPublishers.Dmdata;
using KyoshinEewViewer.Services.TelegramPublishers.JmaXml;
using KyoshinEewViewer.Tests.Services.Mocks;
using Moq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KyoshinEewViewer.Tests.Services;

/// <summary>
/// TelegramProvideServiceの基本的な初期化テスト
/// </summary>
public class TelegramProvideServiceTests : IDisposable
{
	private readonly Mock<ILogger<TelegramProvideService>> _mockLogger;
	private readonly Mock<IServiceProvider> _mockServiceProvider;
	private readonly TelegramProvideService _service;

	public TelegramProvideServiceTests()
	{
		_mockLogger = new Mock<ILogger<TelegramProvideService>>();
		_mockServiceProvider = new Mock<IServiceProvider>();

		_service = new TelegramProvideService(_mockLogger.Object, _mockServiceProvider.Object);
	}

	[Fact]
	public async Task StartAsync_WithCustomPublisherTypes_UsesSpecifiedTypes()
	{
		// Arrange
		var mockPublisher1 = new MockTelegramPublisher
		{
			Name = "HighPriorityPublisher",
			SupportedCategories = [InformationCategory.Earthquake, InformationCategory.Tsunami]
		};
		var mockPublisher2 = new MockTelegramPublisher
		{
			Name = "LowPriorityPublisher",
			SupportedCategories = [InformationCategory.EewForecast, InformationCategory.EewWarning]
		};

		var customTypes = new[]
		{
			typeof(MockTelegramPublisher),
			typeof(MockTelegramPublisher)
		};

		_mockServiceProvider.SetupSequence(x => x.GetService(typeof(MockTelegramPublisher)))
			.Returns(mockPublisher1)
			.Returns(mockPublisher2);

		// Act
		await _service.StartAsync(customTypes);

		// Assert
		Assert.True(mockPublisher1.IsInitialized, "Publisher1が初期化されていること");
		Assert.True(mockPublisher2.IsInitialized, "Publisher2が初期化されていること");
	}

	[Fact]
	public async Task StartAsync_WithoutArgument_UsesDefaultTypes()
	{
		// Arrange
		var mockDmdata = new MockTelegramPublisher
		{
			Name = "DmdataPublisher",
			SupportedCategories = [InformationCategory.Earthquake]
		};
		var mockJma = new MockTelegramPublisher  
		{
			Name = "JmaPublisher",
			SupportedCategories = [InformationCategory.Tsunami]
		};

		_mockServiceProvider.Setup(x => x.GetService(typeof(DmdataRedundantTelegramPublisher)))
			.Returns(mockDmdata);
		_mockServiceProvider.Setup(x => x.GetService(typeof(JmaXmlTelegramPublisher)))
			.Returns(mockJma);

		// Act
		await _service.StartAsync(); // デフォルトを使用

		// Assert
		Assert.True(mockDmdata.IsInitialized, "DmdataPublisherが初期化されていること");
		Assert.True(mockJma.IsInitialized, "JmaPublisherが初期化されていること");
	}

	[Fact]
	public async Task StartAsync_WithInitializationFailure_ContinuesWithOtherPublishers()
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

		_mockServiceProvider.SetupSequence(x => x.GetService(typeof(MockTelegramPublisher)))
			.Returns(failingPublisher)
			.Returns(workingPublisher);

		// Act
		await _service.StartAsync(customTypes);

		// Assert
		Assert.False(failingPublisher.IsInitialized, "失敗したPublisherは初期化されていないこと");
		Assert.True(workingPublisher.IsInitialized, "成功したPublisherは初期化されていること");
	}

	[Fact]
	public async Task StartAsync_PublisherNotInDI_SkipsAndContinues()
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

		_mockServiceProvider.SetupSequence(x => x.GetService(typeof(MockTelegramPublisher)))
			.Returns((object?)null) // 最初のPublisherは見つからない
			.Returns(workingPublisher);

		// Act
		await _service.StartAsync(customTypes);

		// Assert
		Assert.True(workingPublisher.IsInitialized, "見つかったPublisherは初期化されていること");
	}

	[Fact]
	public async Task StartAsync_CalledTwice_ThrowsException()
	{
		// Arrange
		var publisher = new MockTelegramPublisher();
		var customTypes = new[] { typeof(MockTelegramPublisher) };

		_mockServiceProvider.Setup(x => x.GetService(typeof(MockTelegramPublisher)))
			.Returns(publisher);

		// Act
		await _service.StartAsync(customTypes);

		// Assert
		await Assert.ThrowsAsync<InvalidOperationException>(
			async () => await _service.StartAsync(customTypes)
		);
	}

	[Fact]
	public async Task Subscribe_AfterStart_ThrowsException()
	{
		// Arrange
		var publisher = new MockTelegramPublisher();
		var customTypes = new[] { typeof(MockTelegramPublisher) };

		_mockServiceProvider.Setup(x => x.GetService(typeof(MockTelegramPublisher)))
			.Returns(publisher);

		await _service.StartAsync(customTypes);

		// Act & Assert
		Assert.Throws<InvalidOperationException>(() =>
			_service.Subscribe(
				InformationCategory.Earthquake,
				(_, _) => Task.CompletedTask,
				_ => Task.CompletedTask,
				_ => { }
			)
		);
	}

	[Fact]
	public async Task Publishers_AssignedByPriority()
	{
		// Arrange
		var highPriorityPublisher = new MockTelegramPublisher
		{
			Name = "HighPriority",
			SupportedCategories = [InformationCategory.Earthquake, InformationCategory.Tsunami]
		};
		var lowPriorityPublisher = new MockTelegramPublisher
		{
			Name = "LowPriority",
			SupportedCategories = [InformationCategory.Earthquake, InformationCategory.EewForecast]
		};

		// サブスクライバーを登録
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
		_service.Subscribe(
			InformationCategory.EewForecast,
			(_, _) => Task.CompletedTask,
			_ => Task.CompletedTask,
			_ => { }
		);

		var customTypes = new[]
		{
			typeof(MockTelegramPublisher), // 高優先度
			typeof(MockTelegramPublisher)  // 低優先度
		};

		_mockServiceProvider.SetupSequence(x => x.GetService(typeof(MockTelegramPublisher)))
			.Returns(highPriorityPublisher)
			.Returns(lowPriorityPublisher);

		// Act
		await _service.StartAsync(customTypes);

		// Assert
		// Startメソッドが呼ばれたことを確認
		Assert.True(highPriorityPublisher.StartCallCount > 0, $"HighPriorityPublisher.Start was called {highPriorityPublisher.StartCallCount} times");
		Assert.True(lowPriorityPublisher.StartCallCount > 0, $"LowPriorityPublisher.Start was called {lowPriorityPublisher.StartCallCount} times");
		
		// 高優先度PublisherがEarthquakeとTsunamiを処理
		Assert.Contains(InformationCategory.Earthquake, highPriorityPublisher.StartedCategories);
		Assert.Contains(InformationCategory.Tsunami, highPriorityPublisher.StartedCategories);
		// 低優先度PublisherはEewForecastのみ処理（Earthquakeは高優先度が処理）
		Assert.DoesNotContain(InformationCategory.Earthquake, lowPriorityPublisher.StartedCategories);
		Assert.Contains(InformationCategory.EewForecast, lowPriorityPublisher.StartedCategories);
	}

	public void Dispose()
	{
		// TelegramProvideServiceをクリーンアップ
		_service?.Dispose();
	}
}