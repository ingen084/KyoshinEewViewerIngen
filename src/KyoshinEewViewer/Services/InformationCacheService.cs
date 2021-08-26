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

#pragma warning disable CS8618 // null 非許容のフィールドには、コンストラクターの終了時に null 以外の値が入っていなければなりません。Null 許容として宣言することをご検討ください。
		public InformationCacheService()
#pragma warning restore CS8618 // null 非許容のフィールドには、コンストラクターの終了時に null 以外の値が入っていなければなりません。Null 許容として宣言することをご検討ください。
		{
			Logger = LoggingService.CreateLogger(this);

			ReloadCache();
			MessageBus.Current.Listen<ApplicationClosing>().Subscribe(x => CacheDatabase?.Dispose());

			CleanupTelegramCache().ConfigureAwait(false);
		}

		public void ReloadCache()
		{
			CacheDatabase?.Dispose();
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
		}

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
			Logger.LogDebug($"cache used: {key}");
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

			Logger.LogDebug($"cache inserted: {key}");
			_ = CleanupTelegramCache().ConfigureAwait(false);
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
		public SKBitmap SetImageCache(string url, DateTime expireTime, Stream parentStream, Func<SKBitmap, SKBitmap>? processor = null)
		{
			using (parentStream)
			{
				var bitmap = SKBitmap.Decode(parentStream);
				if (processor != null)
					bitmap = processor(bitmap);

				using var stream = new MemoryStream();
				bitmap.Encode(stream, SKEncodedImageFormat.Png, 100);

				if (ImageCacheTable.FindOne(i => i.Url == url) == null)
				{
					stream.Seek(0, SeekOrigin.Begin);
					ImageCacheTable.Insert(new ImageCacheModel(
						url,
						expireTime,
						CompressStream(stream)));
					CacheDatabase.Commit();
				}

				Debug.WriteLine($"image cache inserted: {url}");
				return bitmap;
			}
		}

		private static byte[] CompressStream(Stream body)
		{
			using var outStream = new MemoryStream();
			using (var compressStream = new GZipStream(outStream, CompressionLevel.Optimal))
				body.CopyTo(compressStream);
			return outStream.ToArray();
		}

		private Task CleanupTelegramCache()
			=> Task.Run(() =>
			{
				Logger.LogDebug("telegram cache cleaning...");
				var s = DateTime.Now;
				CacheDatabase.BeginTrans();
				// 2週間以上経過したものを削除
				TelegramCacheTable.DeleteMany(c => c.ArrivalTime < DateTime.Now.AddDays(-14));
				CacheDatabase.Commit();
				Logger.LogDebug($"telegram cache cleaning completed: {(DateTime.Now - s).TotalMilliseconds}ms");
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

	public record TelegramCacheModel(string Key, string Title, DateTime ArrivalTime, byte[] Body);
	public record ImageCacheModel(string Url, DateTime ExpireTime, byte[] Body);
}
