using Microsoft.Extensions.Logging;
using SkiaSharp;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services;

public class InformationCacheService
{
	private static InformationCacheService? _default;
	private static InformationCacheService Default => _default ??= new();

	private ILogger Logger { get; }
	private SHA256 SHA256 { get; } = SHA256.Create();

	private static readonly string ShortCachePath = Path.Join(Path.GetTempPath(), "KyoshinEewViewerIngen", "ShortCache");
	private static readonly string LongCachePath = Path.Join(Path.GetTempPath(), "KyoshinEewViewerIngen", "LongCache");

	public InformationCacheService()
	{
		Logger = LoggingService.CreateLogger(this);
	}

	private static string GetLongCacheFileName(string baseName)
		=> Path.Join(LongCachePath, new(Default.SHA256.ComputeHash(Encoding.UTF8.GetBytes(baseName)).SelectMany(x => x.ToString("x2")).ToArray()));
	private static string GetShortCacheFileName(string baseName)
		=> Path.Join(ShortCachePath, new(Default.SHA256.ComputeHash(Encoding.UTF8.GetBytes(baseName)).SelectMany(x => x.ToString("x2")).ToArray()));

	/// <summary>
	/// Keyを元にキャッシュされたstreamを取得する
	/// </summary>
	public static async Task<Stream?> GetTelegramAsync(string key)
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
				Default.Logger.LogWarning(ex, "LongCacheの読み込みに失敗しています({count})", count);
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
	public static bool ExistsTelegramCache(string key)
	{
		var path = GetLongCacheFileName(key);
		return File.Exists(path);
	}

	/// <summary>
	/// キャッシュする
	/// </summary>
	public static async Task CacheTelegramAsync(string key, Func<Stream> fetcher)
	{
		var path = GetLongCacheFileName(key);
		if (File.Exists(path))
			return;

		using var stream = fetcher();

		if (!Directory.Exists(LongCachePath))
			Directory.CreateDirectory(LongCachePath);

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
				Default.Logger.LogWarning(ex, "LongCacheの書き込みに失敗しています({count})", count);
				await Task.Delay(100);
				count++;
				if (count > 10)
					throw;
			}
		}
	}

	public static async Task<Stream> TryGetOrFetchTelegramAsync(string key, Func<Task<Stream>> fetcher)
	{
		if (await GetTelegramAsync(key) is Stream stream)
			return stream;

		stream = new MemoryStream();
		using (var body = await fetcher())
			await body.CopyToAsync(stream);

		stream.Seek(0, SeekOrigin.Begin);
		if (!Directory.Exists(LongCachePath))
			Directory.CreateDirectory(LongCachePath);

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
				Default.Logger.LogWarning(ex, "LongCacheの書き込みに失敗しています({count})", count);
				await Task.Delay(100);
				count++;
				if (count > 10)
					throw;
			}
		}

		stream.Seek(0, SeekOrigin.Begin);
		return stream;
	}
	public static void DeleteTelegramCache(string key)
	{
		var path = GetLongCacheFileName(key);
		if (File.Exists(path))
			File.Delete(path);
	}

	/// <summary>
	/// URLを元にキャッシュされたstreamを取得する
	/// </summary>
	public static SKBitmap? GetImage(string url)
	{
		var path = GetShortCacheFileName(url);
		if (!File.Exists(path))
			return null;

		return SKBitmap.Decode(path);
	}
	public static async Task<SKBitmap> TryGetOrFetchImageAsync(string url, Func<Task<(SKBitmap, DateTime)>> fetcher)
	{
		if (GetImage(url) is SKBitmap bitmap)
			return bitmap;

		var res = await fetcher();
		bitmap = res.Item1;

		if (!Directory.Exists(ShortCachePath))
			Directory.CreateDirectory(ShortCachePath);
		using var stream = File.OpenWrite(GetShortCacheFileName(url));
		bitmap.Encode(stream, SKEncodedImageFormat.Png, 100);

		return bitmap;
	}

	/// <summary>
	/// URLを元にキャッシュされたstreamを取得する
	/// </summary>
	public static async Task<Stream?> GetImageAsStreamAsync(string url)
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
				Default.Logger.LogWarning(ex, "ShortCacheの読み込みに失敗しています({count})", count);
				await Task.Delay(100);
				count++;
				if (count > 10)
					throw;
			}
		}
	}

	public static async Task<Stream> TryGetOrFetchImageAsStreamAsync(string url, Func<Task<(Stream, DateTime)>> fetcher)
	{
		if (await GetImageAsStreamAsync(url) is Stream stream)
			return stream;

		stream = new MemoryStream();
		var resp = await fetcher();
		using (resp.Item1)
			await resp.Item1.CopyToAsync(stream);

		stream.Seek(0, SeekOrigin.Begin);
		if (!Directory.Exists(ShortCachePath))
			Directory.CreateDirectory(ShortCachePath);
		using var fileStream = File.OpenWrite(GetShortCacheFileName(url));
		await CompressStreamAsync(stream, fileStream);

		stream.Seek(0, SeekOrigin.Begin);
		return stream;
	}
	public static void DeleteImageCache(string url)
	{
		var path = GetShortCacheFileName(url);
		if (File.Exists(path))
			File.Delete(path);
	}

	private static async Task CompressStreamAsync(Stream input, Stream output)
	{
		using var compressStream = new GZipStream(output, CompressionLevel.Optimal);
		await input.CopyToAsync(compressStream);
	}

	public static void CleanupCaches()
	{
		CleanupTelegramCache();
		CleanupImageCache();
	}

	private static void CleanupTelegramCache()
	{
		if (!Directory.Exists(LongCachePath))
			return;

		Default.Logger.LogDebug("telegram cache cleaning...");
		var s = DateTime.Now;
		// 2週間以上経過したものを削除
		foreach (var file in Directory.GetFiles(LongCachePath))
		{
			try
			{
				if (File.GetLastWriteTimeUtc(file) <= DateTime.UtcNow.AddDays(-14))
					File.Delete(file);
			}
			catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
		}
		Default.Logger.LogDebug("telegram cache cleaning completed: {time}ms", (DateTime.Now - s).TotalMilliseconds);
	}
	private static void CleanupImageCache()
	{
		if (!Directory.Exists(ShortCachePath))
			return;

		Default.Logger.LogDebug("image cache cleaning...");
		var s = DateTime.Now;
		// 3時間以上経過したものを削除
		foreach (var file in Directory.GetFiles(ShortCachePath))
		{
			try
			{
				if (File.GetLastWriteTimeUtc(file) <= DateTime.UtcNow.AddHours(-3))
					File.Delete(file);
			}
			catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
		}
		Default.Logger.LogDebug("image cache cleaning completed: {time}ms", (DateTime.Now - s).TotalMilliseconds);
	}
}

public record TelegramCacheModel(string Key, string Title, DateTime ArrivalTime, byte[] Body);
public record ImageCacheModel(string Url, DateTime ExpireTime, byte[] Body);
