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
/// TelegramProvideServiceのシンプルなテスト
/// </summary>
public class TelegramProvideServiceSimpleTests : IDisposable
{
	private readonly Mock<ILogManager> _mockLogManager;
	private readonly Mock<IFullLogger> _mockLogger;
	private readonly Mock<IReadonlyDependencyResolver> _mockServiceProvider;
	private readonly TelegramProvideService _service;

	public TelegramProvideServiceSimpleTests()
	{
		_mockLogManager = new Mock<ILogManager>();
		_mockLogger = new Mock<IFullLogger>();
		
		// GetLoggerメソッドを適切にセットアップするために、実際の実装を提供
		_mockLogManager.Setup(x => x.GetLogger(It.IsAny<Type>()))
			.Returns(_mockLogger.Object);

		_mockServiceProvider = new Mock<IReadonlyDependencyResolver>();

		_service = new TelegramProvideService(_mockLogManager.Object, _mockServiceProvider.Object);
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
		var logManager = CreateMockLogManager();
		var mockServiceProvider = new Mock<IReadonlyDependencyResolver>();
		var service = new TelegramProvideService(logManager, mockServiceProvider.Object);

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

	/// <summary>
	/// テスト用のシンプルなLogManager（モックを使用）
	/// </summary>
	private static ILogManager CreateMockLogManager()
	{
		var mockLogManager = new Mock<ILogManager>();
		var mockLogger = new Mock<IFullLogger>();
		mockLogManager.Setup(x => x.GetLogger(It.IsAny<Type>()))
			.Returns(mockLogger.Object);
		return mockLogManager.Object;
	}
}
