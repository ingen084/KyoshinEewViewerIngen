using DmdataSharp.Interfaces;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.Services.TelegramPublishers.Dmdata;
using Moq;

namespace KyoshinEewViewer.Tests.Services;

/// <summary>
/// DmdataDataProcessorのテスト
/// </summary>
public class DmdataDataProcessorTests : IDisposable
{
	private readonly DmdataDataProcessor _processor;
	private readonly Mock<IDmdataV2ApiClient> _mockApiClient;
	private readonly InformationCacheService _cacheService;

	public DmdataDataProcessorTests()
	{
		var logManager = new DefaultLogManager();
		_cacheService = new InformationCacheService(logManager);
		_processor = new DmdataDataProcessor(logManager, _cacheService);
		_mockApiClient = new Mock<IDmdataV2ApiClient>();
	}

	[Fact(DisplayName = "SetApiClient_APIクライアントが設定される")]
	public void SetApiClient_APIクライアントが設定される()
	{
		// Act
		var exception = Record.Exception(() => _processor.SetApiClient(_mockApiClient.Object));

		// Assert
		Assert.Null(exception);
	}

	[Fact(DisplayName = "ResetState_状態がリセットされる")]
	public void ResetState_状態がリセットされる()
	{
		// Act
		var exception = Record.Exception(() => _processor.ResetState());

		// Assert
		Assert.Null(exception);
	}

	[Fact(DisplayName = "ProcessWebSocketDataAsync_nullデータ_nullを返す")]
	public async Task ProcessWebSocketDataAsync_nullデータ_nullを返す()
	{
		// Act
		var result = await _processor.ProcessWebSocketDataAsync(null);

		// Assert
		Assert.Null(result);
	}

	[Fact(DisplayName = "IsSubscribedType_購読中タイプ_Trueを返す")]
	public void IsSubscribedType_購読中タイプ_Trueを返す()
	{
		// Arrange
		var subscribingCategories = new[] { InformationCategory.Earthquake };

		// Act
		var result = DmdataDataProcessor.IsSubscribedType("VXSE51", subscribingCategories);

		// Assert
		Assert.True(result);
	}

	[Fact(DisplayName = "IsSubscribedType_非購読タイプ_Falseを返す")]
	public void IsSubscribedType_非購読タイプ_Falseを返す()
	{
		// Arrange
		var subscribingCategories = new[] { InformationCategory.Earthquake };

		// Act
		var result = DmdataDataProcessor.IsSubscribedType("UNKNOWN", subscribingCategories);

		// Assert
		Assert.False(result);
	}

	[Fact(DisplayName = "GetCategoryFromType_地震タイプ_Earthquakeカテゴリを返す")]
	public void GetCategoryFromType_地震タイプ_Earthquakeカテゴリを返す()
	{
		// Act
		var result = DmdataDataProcessor.GetCategoryFromType("VXSE51");

		// Assert
		Assert.Equal(InformationCategory.Earthquake, result);
	}

	[Fact(DisplayName = "GetCategoryFromType_EEW予報タイプ_EewForecastカテゴリを返す")]
	public void GetCategoryFromType_EEW予報タイプ_EewForecastカテゴリを返す()
	{
		// Act
		var result = DmdataDataProcessor.GetCategoryFromType("VXSE42");

		// Assert
		Assert.Equal(InformationCategory.EewForecast, result);
	}

	[Fact(DisplayName = "GetCategoryFromType_EEW警報タイプ_EewWarningカテゴリを返す")]
	public void GetCategoryFromType_EEW警報タイプ_EewWarningカテゴリを返す()
	{
		// Act
		var result = DmdataDataProcessor.GetCategoryFromType("VXSE43");

		// Assert
		Assert.Equal(InformationCategory.EewWarning, result);
	}

	[Fact(DisplayName = "GetCategoryFromType_津波タイプ_Tsunamiカテゴリを返す")]
	public void GetCategoryFromType_津波タイプ_Tsunamiカテゴリを返す()
	{
		// Act
		var result = DmdataDataProcessor.GetCategoryFromType("VTSE41");

		// Assert
		Assert.Equal(InformationCategory.Tsunami, result);
	}

	[Fact(DisplayName = "GetCategoryFromType_台風タイプ_Typhoonカテゴリを返す")]
	public void GetCategoryFromType_台風タイプ_Typhoonカテゴリを返す()
	{
		// Act
		var result = DmdataDataProcessor.GetCategoryFromType("VPTW60");

		// Assert
		Assert.Equal(InformationCategory.Typhoon, result);
	}

	[Fact(DisplayName = "GetCategoryFromType_未知のタイプ_nullを返す")]
	public void GetCategoryFromType_未知のタイプ_nullを返す()
	{
		// Act
		var result = DmdataDataProcessor.GetCategoryFromType("UNKNOWN");

		// Assert
		Assert.Null(result);
	}

	[Fact(DisplayName = "GetTypesFromCategory_Earthquakeカテゴリ_地震タイプ配列を返す")]
	public void GetTypesFromCategory_Earthquakeカテゴリ_地震タイプ配列を返す()
	{
		// Act
		var result = DmdataDataProcessor.GetTypesFromCategory(InformationCategory.Earthquake);

		// Assert
		Assert.NotEmpty(result);
		Assert.Contains("VXSE51", result);
		Assert.Contains("VXSE52", result);
		Assert.Contains("VXSE53", result);
	}

	[Fact(DisplayName = "GetTypesFromCategory_EewForecastカテゴリ_EEW予報タイプ配列を返す")]
	public void GetTypesFromCategory_EewForecastカテゴリ_EEW予報タイプ配列を返す()
	{
		// Act
		var result = DmdataDataProcessor.GetTypesFromCategory(InformationCategory.EewForecast);

		// Assert
		Assert.NotEmpty(result);
		Assert.Contains("VXSE42", result);
		Assert.Contains("VXSE45", result);
	}

	[Fact(DisplayName = "FetchListAsync_APIクライアント未設定_例外をスロー")]
	public async Task FetchListAsync_APIクライアント未設定_例外をスロー()
	{
		// Act & Assert
		await Assert.ThrowsAsync<DmdataSharp.Exceptions.DmdataException>(
			async () => await _processor.FetchListAsync(null, false, false)
		);
	}

	[Fact(DisplayName = "FetchContentAsync_APIクライアント未設定_例外をスロー")]
	public async Task FetchContentAsync_APIクライアント未設定_例外をスロー()
	{
		// Act & Assert
		await Assert.ThrowsAsync<Exception>(
			async () => await _processor.FetchContentAsync("test-key")
		);
	}

	public void Dispose()
	{
		GC.SuppressFinalize(this);
	}
}
