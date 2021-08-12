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

		private LiteDatabase CacheDatabase { get; }
		private ILiteCollection<InformationCacheModel> CacheTable { get; }
		private ILiteCollection<ImageCacheModel> ImageCacheTable { get; }

		private ILogger Logger { get; }

		public InformationCacheService()
		{
			Logger = LoggingService.CreateLogger(this);

			CacheDatabase = new LiteDatabase("cache.db");
			CacheTable = CacheDatabase.GetCollection<InformationCacheModel>();
			CacheTable.EnsureIndex(x => x.Key, true);
			ImageCacheTable = CacheDatabase.GetCollection<ImageCacheModel>();
			ImageCacheTable.EnsureIndex(x => x.Url, true);
			MessageBus.Current.Listen<ApplicationClosing>().Subscribe(x => CacheDatabase.Dispose());

			CleanupCache().ConfigureAwait(false);
		}

		/// <summary>
		/// Keyを元にキャッシュされたstreamを取得する
		/// </summary>
		public bool TryGetContent(string key, out Stream stream)
		{
			var cache = CacheTable.FindOne(i => i.Key == key);
			if (cache == null)
			{
#pragma warning disable CS8625 // falseなので普通にnullを代入する
				stream = null;
				return false;
#pragma warning restore CS8625
			}
			Logger.LogDebug($"cache used: {key}");
			var memStream = new MemoryStream(cache.Body);
			stream = new GZipStream(memStream, CompressionMode.Decompress);
			return true;
		}

		public async Task<Stream> TryGetOrFetchContentAsync(string key, string title, DateTime arrivalTime, Func<Task<Stream>> fetcher)
		{
			if (TryGetContent(key, out var stream))
				return stream;

			stream = new MemoryStream();
			using (var body = await fetcher())
				await body.CopyToAsync(stream);

			stream.Seek(0, SeekOrigin.Begin);
			CacheTable.Insert(new InformationCacheModel(
				key,
				title,
				arrivalTime,
				CompressStream(stream)));
			CacheDatabase.Commit();

			Logger.LogDebug($"cache inserted: {key}");
			_ = CleanupCache().ConfigureAwait(false);
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
			Debug.WriteLine($"image cache used: {url}");
			using var memStream = new MemoryStream(cache.Body);
			using var stream = new GZipStream(memStream, CompressionMode.Decompress);
			bitmap = SKBitmap.Decode(stream);
			return true;
		}
		public SKBitmap SetImageCache(string url, DateTime expireTime, Stream parentStream)
		{
			if (TryGetImage(url, out var bitmap))
				return bitmap;

			using var stream = new MemoryStream();
			parentStream.CopyTo(stream);

			stream.Seek(0, SeekOrigin.Begin);
			ImageCacheTable.Insert(new ImageCacheModel(
				url,
				expireTime,
				CompressStream(stream)));
			CacheDatabase.Commit();

			Debug.WriteLine($"image cache inserted: {url}");
			stream.Seek(0, SeekOrigin.Begin);
			return SKBitmap.Decode(stream);
		}

		private static byte[] CompressStream(Stream body)
		{
			using var outStream = new MemoryStream();
			using (var compressStream = new GZipStream(outStream, CompressionLevel.Optimal))
				body.CopyTo(compressStream);
			return outStream.ToArray();
		}

		private Task CleanupCache()
			=> Task.Run(() =>
			{
				Logger.LogDebug("cache cleaning...");
				var s = DateTime.Now;
				CacheDatabase.BeginTrans();
				// 2週間以上経過したものを削除
				CacheTable.DeleteMany(c => c.ArrivalTime < DateTime.Now.AddDays(-14));
				CacheDatabase.Commit();
				Logger.LogDebug($"cache cleaning completed: {(DateTime.Now - s).TotalMilliseconds}ms");
			});
		public void CleanupImageCache()
			=> Task.Run(() =>
			{
				Logger.LogDebug("image cache cleaning...");
				var s = DateTime.Now;
				CacheDatabase.BeginTrans();
				// 期限が切れたものを削除
				ImageCacheTable.DeleteMany(c => c.ExpireTime < DateTime.Now);
				CacheDatabase.Commit();
				Logger.LogDebug($"image cache cleaning completed: {(DateTime.Now - s).TotalMilliseconds}ms");
			});
	}

	public record InformationCacheModel(string Key, string Title, DateTime ArrivalTime, byte[] Body);
	public record ImageCacheModel(string Url, DateTime ExpireTime, byte[] Body);
}
