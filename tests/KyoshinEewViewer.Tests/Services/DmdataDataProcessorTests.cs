using DmdataSharp.ApiResponses.V2;
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

	[Fact(DisplayName = "IsSubscribedType_EEW警報タイプを警報カテゴリ購読時のみTrueにする")]
	public void IsSubscribedType_EEW警報タイプを警報カテゴリ購読時のみTrueにする()
	{
		Assert.True(DmdataDataProcessor.IsSubscribedType("VXSE43", [InformationCategory.EewWarning]));
		Assert.False(DmdataDataProcessor.IsSubscribedType("VXSE43", [InformationCategory.EewForecast]));
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

	[Fact(DisplayName = "GetTypesFromCategory_EewWarningカテゴリ_EEW警報タイプ配列を返す")]
	public void GetTypesFromCategory_EewWarningカテゴリ_EEW警報タイプ配列を返す()
	{
		// Act
		var result = DmdataDataProcessor.GetTypesFromCategory(InformationCategory.EewWarning);

		// Assert
		Assert.Equal(["VXSE43"], result);
	}

	[Fact(DisplayName = "FetchListAsync_APIクライアント未設定_例外をスロー")]
	public async Task FetchListAsync_APIクライアント未設定_例外をスロー()
	{
		// Act & Assert
		await Assert.ThrowsAsync<DmdataSharp.Exceptions.DmdataException>(
			async () => await _processor.FetchListAsync(null, false, false)
		);
	}

	[Fact(DisplayName = "FetchListAsync_タイプが5個を超えるカテゴリ_5個以下に分割リクエストされ受信時刻の新しい順に切り詰められる")]
	public async Task FetchListAsync_タイプが5個を超えるカテゴリ_5個以下に分割リクエストされ受信時刻の新しい順に切り詰められる()
	{
		// Arrange
		var baseTime = new DateTime(2026, 7, 1, 12, 0, 0);

		static TelegramListResponse.Item CreateXmlItem(string id, string type, DateTime time) => new()
		{
			Id = id,
			Format = "xml",
			ReceivedTime = time,
			Head = new TelegramListResponse.Head { Type = type, Time = time },
			XmlReport = new TelegramXmldata
			{
				Control = new TelegramXmldata.XmlControl { Title = "テスト電文", DateTime = time },
			},
		};
		static TelegramListResponse.Item CreateBinaryItem(string id, DateTime time) => new()
		{
			Id = id,
			Format = "binary",
			ReceivedTime = time,
			Head = new TelegramListResponse.Head { Type = "IXAC41", Time = time },
		};

		// XML電文チャンク: 直近50件(1分間隔)
		var xmlItems = Enumerable.Range(0, 50)
			.Select(i => CreateXmlItem($"xml-{i}", "VXSE53", baseTime.AddMinutes(-i)))
			.ToArray();
		// IXAC41チャンク: 直近1件 + XML電文チャンクの範囲より古い2件
		var binaryItems = new[]
		{
			CreateBinaryItem("ixac-recent", baseTime.AddMinutes(-10)),
			CreateBinaryItem("ixac-old-1", baseTime.AddDays(-10)),
			CreateBinaryItem("ixac-old-2", baseTime.AddDays(-20)),
		};

		var capturedTypes = new List<string?>();
		_mockApiClient
			.Setup(c => c.GetTelegramListAsync(It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>()))
			.Callback<string?, bool, string, string?, string?, int>((type, _, _, _, _, _) => capturedTypes.Add(type))
			.ReturnsAsync((string? type, bool _, string _, string? _, string? _, int _) => new TelegramListResponse
			{
				Status = "ok",
				Items = type?.Contains("IXAC41") == true ? binaryItems : xmlItems,
				NextPoolingInterval = 1000,
			});
		_processor.SetApiClient(_mockApiClient.Object);

		// Act
		var (infos, _) = await _processor.FetchListAsync(InformationCategory.Earthquake, false, false);

		// Assert
		// typeパラメータは5個以下に分割され、全タイプが漏れなく指定されている
		Assert.True(capturedTypes.Count > 1);
		Assert.All(capturedTypes, t => Assert.InRange(t!.Split(',').Length, 1, 5));
		Assert.Equal(
			DmdataDataProcessor.GetTypesFromCategory(InformationCategory.Earthquake).Order(),
			capturedTypes.SelectMany(t => t!.Split(',')).Order());

		// 全タイプを一度に指定した場合と同じく、受信時刻の新しい順に50件へ切り詰められる
		Assert.Equal(50, infos.Length);
		Assert.Contains(infos, i => i.key == "ixac-recent");
		Assert.DoesNotContain(infos, i => i.key == "ixac-old-1");
		Assert.DoesNotContain(infos, i => i.key == "ixac-old-2");
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
