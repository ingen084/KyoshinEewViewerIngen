using KyoshinEewViewer.Core;
using SkiaSharp;
using Splat;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services;

public class InformationCacheService
{
	private ILogger Logger { get; }
	public Timer ClearCacheTimer { get; }
	private SHA256 Sha256 { get; } = SHA256.Create();

	private readonly string _shortCachePath = Path.Join(Path.GetTempPath(), "KyoshinEewViewerIngen", "ShortCache");
	private readonly string _longCachePath = Path.Join(Path.GetTempPath(), "KyoshinEewViewerIngen", "LongCache");

	public InformationCacheService(ILogManager logManager)
	{
		SplatRegistrations.RegisterLazySingleton<InformationCacheService>();

		Logger = logManager.GetLogger<InformationCacheService>();
		ClearCacheTimer = new(s => CleanupCaches(), null, TimeSpan.FromMinutes(1), TimeSpan.FromHours(1));
	}

	private string GetLongCacheFileName(string baseName)
	{
		lock (Sha256)
			return Path.Join(_longCachePath, new(Sha256.ComputeHash(Encoding.UTF8.GetBytes(baseName)).SelectMany(x => x.ToString("x2")).ToArray()));
	}
	private string GetShortCacheFileName(string baseName)
	{
		lock (Sha256)
			return Path.Join(_shortCachePath, new(Sha256.ComputeHash(Encoding.UTF8.GetBytes(baseName)).SelectMany(x => x.ToString("x2")).ToArray()));
	}

	/// <summary>
	/// Keyを元にキャッシュされたstreamを取得する
	/// </summary>
	public async Task<Stream?> GetTelegramAsync(string key)
	{
		var path = GetLongCacheFileName(key);
		if (!File.Exists(path))
			return null;

		var count = 0;
		while (true)
		{
			try
			{
				// 参照があった場合はキャッシュの期限を延長しておく
				try
				{
					File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
				}
				catch (IOException) { }

				var fileStream = File.OpenRead(path);
				return new GZipStream(fileStream, CompressionMode.Decompress);
			}
			catch (IOException ex)
			{
				Logger.LogWarning(ex, $"LongCacheの読み込みに失敗しています({count})");
				await Task.Delay(100);
				count++;
				if (count > 10)
					throw;
			}
		}
	}

	/// <summary>
	/// Keyを元にキャッシュが存在するか確認する
	/// </summary>
	public bool ExistsTelegramCache(string key)
	{
		var path = GetLongCacheFileName(key);
		return File.Exists(path);
	}

	/// <summary>
	/// キャッシュする
	/// </summary>
	public async Task CacheTelegramAsync(string key, Func<Stream> fetcher)
	{
		var path = GetLongCacheFileName(key);
		if (File.Exists(path))
			return;

		using var stream = fetcher();

		if (!Directory.Exists(_longCachePath))
			Directory.CreateDirectory(_longCachePath);

		var count = 0;
		while (true)
		{
			try
			{
				using var fileStream = File.OpenWrite(GetLongCacheFileName(key));
				await CompressStreamAsync(stream, fileStream);
				break;
			}
			catch (IOException ex)
			{
				Logger.LogWarning(ex, $"LongCacheの書き込みに失敗しています({count})");
				await Task.Delay(100);
				count++;
				if (count > 10)
					throw;
			}
		}
	}

	public async Task<Stream> TryGetOrFetchTelegramAsync(string key, Func<Task<Stream>> fetcher)
	{
		if (await GetTelegramAsync(key) is { } stream)
			return stream;

		stream = new MemoryStream();
		using (var body = await fetcher())
			await body.CopyToAsync(stream);

		stream.Seek(0, SeekOrigin.Begin);
		if (!Directory.Exists(_longCachePath))
			Directory.CreateDirectory(_longCachePath);

		var count = 0;
		while (true)
		{
			try
			{
				using var fileStream = File.OpenWrite(GetLongCacheFileName(key));
				await CompressStreamAsync(stream, fileStream);
				break;
			}
			catch (IOException ex)
			{
				Logger.LogWarning(ex, $"LongCacheの書き込みに失敗しています({count})");
				await Task.Delay(100);
				count++;
				if (count > 10)
					throw;
			}
		}

		stream.Seek(0, SeekOrigin.Begin);
		return stream;
	}
	public void DeleteTelegramCache(string key)
	{
		var path = GetLongCacheFileName(key);
		if (File.Exists(path))
			File.Delete(path);
	}

	/// <summary>
	/// URLを元にキャッシュされたstreamを取得する
	/// </summary>
	public SKBitmap? GetImage(string url)
	{
		try
		{
			var path = GetShortCacheFileName(url);
			if (!File.Exists(path))
				return null;

			return SKBitmap.Decode(path);
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "GetImage中に例外");
			return null;
		}
	}
	public async Task<SKBitmap> TryGetOrFetchImageAsync(string url, Func<Task<(SKBitmap, DateTime)>> fetcher)
	{
		if (GetImage(url) is { } bitmap)
			return bitmap;

		var res = await fetcher();
		bitmap = res.Item1;

		if (!Directory.Exists(_shortCachePath))
			Directory.CreateDirectory(_shortCachePath);
		using var stream = File.OpenWrite(GetShortCacheFileName(url));
		bitmap.Encode(stream, SKEncodedImageFormat.Png, 100);

		return bitmap;
	}

	/// <summary>
	/// URLを元にキャッシュされたstreamを取得する
	/// </summary>
	public async Task<Stream?> GetImageAsStreamAsync(string url)
	{
		var path = GetShortCacheFileName(url);
		if (!File.Exists(path))
			return null;

		var count = 0;
		while (true)
		{
			try
			{
				var fileStream = File.OpenRead(path);
				return new GZipStream(fileStream, CompressionMode.Decompress);
			}
			catch (IOException ex)
			{
				Logger.LogWarning(ex, $"ShortCacheの読み込みに失敗しています({count})");
				await Task.Delay(100);
				count++;
				if (count > 10)
					throw;
			}
		}
	}

	public async Task<Stream> TryGetOrFetchImageAsStreamAsync(string url, Func<Task<(Stream, DateTime)>> fetcher)
	{
		if (await GetImageAsStreamAsync(url) is { } stream)
			return stream;

		stream = new MemoryStream();
		var resp = await fetcher();
		using (resp.Item1)
			await resp.Item1.CopyToAsync(stream);

		stream.Seek(0, SeekOrigin.Begin);
		if (!Directory.Exists(_shortCachePath))
			Directory.CreateDirectory(_shortCachePath);
		using var fileStream = File.OpenWrite(GetShortCacheFileName(url));
		await CompressStreamAsync(stream, fileStream);

		stream.Seek(0, SeekOrigin.Begin);
		return stream;
	}
	public void DeleteImageCache(string url)
	{
		var path = GetShortCacheFileName(url);
		if (File.Exists(path))
			File.Delete(path);
	}

	private async Task CompressStreamAsync(Stream input, Stream output)
	{
		using var compressStream = new GZipStream(output, CompressionLevel.Optimal);
		await input.CopyToAsync(compressStream);
	}

	public void CleanupCaches()
	{
		CleanupTelegramCache();
		CleanupImageCache();
	}

	private void CleanupTelegramCache()
	{
		if (!Directory.Exists(_longCachePath))
			return;

		Logger.LogDebug("telegram cache cleaning...");
		var s = DateTime.Now;
		// 2週間以上経過したものを削除
		foreach (var file in Directory.GetFiles(_longCachePath))
		{
			try
			{
				if (File.GetLastWriteTimeUtc(file) <= DateTime.UtcNow.AddDays(-14))
					File.Delete(file);
			}
			catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
		}
		Logger.LogDebug($"telegram cache cleaning completed: {(DateTime.Now - s).TotalMilliseconds}ms");
	}
	private void CleanupImageCache()
	{
		if (!Directory.Exists(_shortCachePath))
			return;

		Logger.LogDebug("image cache cleaning...");
		var s = DateTime.Now;
		// 3時間以上経過したものを削除
		foreach (var file in Directory.GetFiles(_shortCachePath))
		{
			try
			{
				if (File.GetLastWriteTimeUtc(file) <= DateTime.UtcNow.AddHours(-3))
					File.Delete(file);
			}
			catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
		}
		Logger.LogDebug($"image cache cleaning completed: {(DateTime.Now - s).TotalMilliseconds}ms");
	}
}
