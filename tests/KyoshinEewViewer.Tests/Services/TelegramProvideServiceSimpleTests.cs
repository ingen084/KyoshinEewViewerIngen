using KyoshinEewViewer.Services;
using Moq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KyoshinEewViewer.Tests.Services;

/// <summary>
/// TelegramProvideServiceのシンプルなテスト
/// </summary>
public class TelegramProvideServiceSimpleTests : IDisposable
{
	private readonly Mock<ILogger<TelegramProvideService>> _mockLogger;
	private readonly Mock<IServiceProvider> _mockServiceProvider;
	private readonly TelegramProvideService _service;

	public TelegramProvideServiceSimpleTests()
	{
		_mockLogger = new Mock<ILogger<TelegramProvideService>>();
		_mockServiceProvider = new Mock<IServiceProvider>();

		_service = new TelegramProvideService(_mockLogger.Object, _mockServiceProvider.Object);
	}

	[Fact(DisplayName = "デフォルトプロバイダタイプに期待されるタイプが含まれている")]
	public void DefaultPublisherTypes_ContainsExpectedTypes()
	{
		// Assert
		Assert.Contains(typeof(KyoshinEewViewer.Services.TelegramPublishers.Dmdata.DmdataRedundantTelegramPublisher), 
			TelegramProvideService.DefaultPublisherTypes);
		Assert.Contains(typeof(KyoshinEewViewer.Services.TelegramPublishers.JmaXml.JmaXmlTelegramPublisher), 
			TelegramProvideService.DefaultPublisherTypes);
	}

	[Fact(DisplayName = "サービス開始前のサブスクライブが成功する")]
	public void Subscribe_BeforeStart_Success()
	{
		// Arrange
		var mockServiceProvider = new Mock<IServiceProvider>();
		var service = new TelegramProvideService(new Mock<ILogger<TelegramProvideService>>().Object, mockServiceProvider.Object);

		// Act & Assert - 例外が発生しないこと
		service.Subscribe(
			InformationCategory.Earthquake,
			(_, _) => Task.CompletedTask,
			_ => Task.CompletedTask,
			_ => { }
		);
	}



	public void Dispose()
	{
		_service?.Dispose();
	}

}
