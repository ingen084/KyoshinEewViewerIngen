using KyoshinEewViewer.Core.Models.KyoshinMonitorObservationPoint;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace ShakeDetectionProducer.Services;

/// <summary>
/// 観測点データを読み込むシンプルなサービス
/// </summary>
public class SimpleObservationPointsLoader
{
	private static HttpClient HttpClient { get; } = new()
	{
		Timeout = TimeSpan.FromSeconds(30)
	};

	private ILogger<SimpleObservationPointsLoader> Logger { get; }

	/// <summary>
	/// 読み込んだファイルのヘッダー情報
	/// </summary>
	public ObservationPointsFileHeader? Header { get; private set; }

	public SimpleObservationPointsLoader(ILogger<SimpleObservationPointsLoader> logger)
	{
		Logger = logger;
	}

	/// <summary>
	/// ファイルパスまたはURLから観測点データを読み込む
	/// </summary>
	/// <param name="pathOrUrl">ファイルパスまたはURL</param>
	/// <returns>観測点データの配列</returns>
	public async Task<ObservationPointV2[]> LoadAsync(string pathOrUrl)
	{
		if (pathOrUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
			pathOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
		{
			return await LoadFromUrlAsync(pathOrUrl);
		}

		return await LoadFromFileAsync(pathOrUrl);
	}

	/// <summary>
	/// ファイルから観測点データを読み込む
	/// </summary>
	private async Task<ObservationPointV2[]> LoadFromFileAsync(string path)
	{
		Logger.LogInformation("ファイルから観測点データを読み込みます: {Path}", path);

		await using var stream = File.OpenRead(path);
		using var reader = new ObservationPointsFileReader(stream);

		Header = await reader.ReadHeader();
		var data = await reader.ReadData(Header.CompressionMode);

		Logger.LogInformation("観測点データ読み込み完了: {Count}件 (作成日時: {PackedAt})", data.Length, Header.PackedAt);

		return data;
	}

	/// <summary>
	/// URLから観測点データを読み込む
	/// </summary>
	private async Task<ObservationPointV2[]> LoadFromUrlAsync(string url)
	{
		Logger.LogInformation("URLから観測点データを読み込みます: {Url}", url);

		await using var response = await HttpClient.GetStreamAsync(url);
		using var memoryStream = new MemoryStream();
		await response.CopyToAsync(memoryStream);
		memoryStream.Seek(0, SeekOrigin.Begin);

		using var reader = new ObservationPointsFileReader(memoryStream);

		Header = await reader.ReadHeader();
		var data = await reader.ReadData(Header.CompressionMode);

		Logger.LogInformation("観測点データ読み込み完了: {Count}件 (作成日時: {PackedAt})", data.Length, Header.PackedAt);

		return data;
	}
}
