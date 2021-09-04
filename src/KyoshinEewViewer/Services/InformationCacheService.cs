using KyoshinEewViewer.Core.Models.Events;
using LiteDB;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using SkiaSharp;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services
{
	public class InformationCacheService
	{
		private static InformationCacheService? _default;
		public static InformationCacheService Default => _default ??= new InformationCacheService();

		private LiteDatabase CacheDatabase { get; set; }

		private ILiteCollection<TelegramCacheModel> TelegramCacheTable { get; set; }
		private ILiteCollection<ImageCacheModel> ImageCacheTable { get; set; }

		private ILogger Logger { get; }

		public InformationCacheService()
		{
			Logger = LoggingService.CreateLogger(this);

			try
			{
				CacheDatabase = new LiteDatabase("cache.db");
			}
			catch (LiteException)
			{
				File.Delete("cache.db");
				CacheDatabase = new LiteDatabase("cache.db");
			}
			TelegramCacheTable = CacheDatabase.GetCollection<TelegramCacheModel>();
			TelegramCacheTable.EnsureIndex(x => x.Key, true);
			ImageCacheTable = CacheDatabase.GetCollection<ImageCacheModel>();
			ImageCacheTable.EnsureIndex(x => x.Url, true);
			Rebuild();

			MessageBus.Current.Listen<ApplicationClosing>().Subscribe(x => CacheDatabase?.Dispose());
		}

		public void Rebuild()
			=> CacheDatabase.Rebuild();

		/// <summary>
		/// Keyを元にキャッシュされたstreamを取得する
		/// </summary>
		public bool TryGetTelegram(string key, out Stream stream)
		{
			var cache = TelegramCacheTable.FindOne(i => i.Key == key);
			if (cache == null)
			{
#pragma warning disable CS8625 // falseなので普通にnullを代入する
				stream = null;
				return false;
#pragma warning restore CS8625
			}
			var memStream = new MemoryStream(cache.Body);
			stream = new GZipStream(memStream, CompressionMode.Decompress);
			return true;
		}

		public async Task<Stream> TryGetOrFetchTelegramAsync(string key, string title, DateTime arrivalTime, Func<Task<Stream>> fetcher)
		{
			if (TryGetTelegram(key, out var stream))
				return stream;

			stream = new MemoryStream();
			using (var body = await fetcher())
				await body.CopyToAsync(stream);

			stream.Seek(0, SeekOrigin.Begin);
			TelegramCacheTable.Insert(new TelegramCacheModel(
				key,
				title,
				arrivalTime,
				CompressStream(stream)));
			CacheDatabase.Commit();

			stream.Seek(0, SeekOrigin.Begin);
			return stream;
		}

		/// <summary>
		/// URLを元にキャッシュされたstreamを取得する
		/// </summary>
		public bool TryGetImage(string url, out SKBitmap bitmap)
		{
			var cache = ImageCacheTable.FindOne(i => i.Url == url);
			if (cache == null)
			{
#pragma warning disable CS8625 // falseなので普通にnullを代入する
				bitmap = null;
				return false;
#pragma warning restore CS8625
			}
			using var memStream = new MemoryStream(cache.Body);
			using var stream = new GZipStream(memStream, CompressionMode.Decompress);
			bitmap = SKBitmap.Decode(stream);
			return true;
		}
		public async Task<SKBitmap> TryGetOrFetchImageAsync(string url, Func<Task<(SKBitmap, DateTime)>> fetcher)
		{
			if (TryGetImage(url, out var bitmap))
				return bitmap;

			var res = await fetcher();
			bitmap = res.Item1;

			using var stream = new MemoryStream();
			bitmap.Encode(stream, SKEncodedImageFormat.Png, 100);

			stream.Seek(0, SeekOrigin.Begin);
			ImageCacheTable.Insert(new ImageCacheModel(
				url,
				res.Item2,
				CompressStream(stream)));
			CacheDatabase.Commit();
			return bitmap;
		}

		/// <summary>
		/// URLを元にキャッシュされたstreamを取得する
		/// </summary>
		public bool TryGetImageAsStream(string url, out Stream stream)
		{
			var cache = ImageCacheTable.FindOne(i => i.Url == url);
			if (cache == null)
			{
#pragma warning disable CS8625 // falseなので普通にnullを代入する
				stream = null;
				return false;
#pragma warning restore CS8625
			}
			var memStream = new MemoryStream(cache.Body);
			stream = new GZipStream(memStream, CompressionMode.Decompress);
			return true;
		}

		public async Task<Stream> TryGetOrFetchImageAsStreamAsync(string url, Func<Task<(Stream, DateTime)>> fetcher)
		{
			if (TryGetImageAsStream(url, out var stream))
				return stream;

			stream = new MemoryStream();
			var resp = await fetcher();
			using (resp.Item1)
				await resp.Item1.CopyToAsync(stream);

			stream.Seek(0, SeekOrigin.Begin);
			ImageCacheTable.Insert(new ImageCacheModel(
				url,
				resp.Item2,
				CompressStream(stream)));
			CacheDatabase.Commit();

			stream.Seek(0, SeekOrigin.Begin);
			return stream;
		}

		private static byte[] CompressStream(Stream body)
		{
			using var outStream = new MemoryStream();
			using (var compressStream = new GZipStream(outStream, CompressionLevel.Optimal))
				body.CopyTo(compressStream);
			return outStream.ToArray();
		}

		public void CleanupCaches()
		{
			CleanupTelegramCache();
			CleanupImageCache();
		}
		private void CleanupTelegramCache()
		{
			Logger.LogDebug("telegram cache cleaning...");
			var s = DateTime.Now;
			CacheDatabase.BeginTrans();
			// 2週間以上経過したものを削除
			TelegramCacheTable.DeleteMany(c => c.ArrivalTime < DateTime.Now.AddDays(-14));
			Logger.LogDebug($"telegram cache cleaning completed: {(DateTime.Now - s).TotalMilliseconds}ms");
		}
		private void CleanupImageCache()
		{
			Logger.LogDebug("image cache cleaning...");
			var s = DateTime.Now;
			CacheDatabase.BeginTrans();
			// 期限が切れたものを削除
			ImageCacheTable.DeleteMany(c => c.ExpireTime < DateTime.Now);
			Logger.LogDebug($"image cache cleaning completed: {(DateTime.Now - s).TotalMilliseconds}ms");
		}
	}

	public record TelegramCacheModel(string Key, string Title, DateTime ArrivalTime, byte[] Body);
	public record ImageCacheModel(string Url, DateTime ExpireTime, byte[] Body);
}
