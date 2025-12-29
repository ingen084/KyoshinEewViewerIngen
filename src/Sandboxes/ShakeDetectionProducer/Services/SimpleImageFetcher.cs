using KyoshinMonitorLib;
using KyoshinMonitorLib.UrlGenerator;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace ShakeDetectionProducer.Services;

/// <summary>
/// 強震モニタ画像を取得するシンプルなサービス
/// </summary>
public class SimpleImageFetcher : IDisposable
{
	private static HttpClient HttpClient { get; } = new(new HttpClientHandler
	{
		AutomaticDecompression = DecompressionMethods.All,
		MaxConnectionsPerServer = 1,
	})
	{
		Timeout = TimeSpan.FromSeconds(2)
	};

	private ILogger<SimpleImageFetcher> Logger { get; }
	private bool _disposed;

	/// <summary>
	/// エラー発生時のイベント
	/// </summary>
	public event Action<string>? ErrorOccurred;

	public SimpleImageFetcher(ILogger<SimpleImageFetcher> logger)
	{
		Logger = logger;
	}

	/// <summary>
	/// 指定時刻の強震モニタ画像を取得する
	/// </summary>
	/// <param name="time">取得対象の時刻</param>
	/// <returns>取得した画像、失敗時はnull</returns>
	public async Task<SKBitmap?> FetchImageAsync(DateTime time)
	{
		var sw = Stopwatch.StartNew();
		var imageUrl = WebApiUrlGenerator.Generate(WebApiUrlType.RealtimeImg, time, RealtimeDataType.Shindo, false);

		try
		{
			using var response = await HttpClient.GetAsync(imageUrl);

			if (response.StatusCode != HttpStatusCode.OK)
			{
				var message = $"{time:HH:mm:ss} 画像取得失敗: HTTP {(int)response.StatusCode}";
				Logger.LogWarning(message);
				ErrorOccurred?.Invoke(message);
				return null;
			}

			await using var stream = await response.Content.ReadAsStreamAsync();
			var bitmap = SKBitmap.Decode(stream);

			if (bitmap == null)
			{
				var message = $"{time:HH:mm:ss} 画像デコード失敗";
				Logger.LogWarning(message);
				ErrorOccurred?.Invoke(message);
				return null;
			}

			sw.Stop();
			Logger.LogDebug("画像取得完了: {Time:HH:mm:ss} ({Duration}ms)", time, sw.ElapsedMilliseconds);

			return bitmap;
		}
		catch (TaskCanceledException)
		{
			var message = $"{time:HH:mm:ss} タイムアウト";
			Logger.LogWarning(message);
			ErrorOccurred?.Invoke(message);
			return null;
		}
		catch (HttpRequestException ex)
		{
			var message = $"{time:HH:mm:ss} エラー: {ex.Message}";
			Logger.LogWarning(ex, message);
			ErrorOccurred?.Invoke(message);
			return null;
		}
	}

	public void Dispose()
	{
		if (_disposed)
			return;
		_disposed = true;
		GC.SuppressFinalize(this);
	}
}
