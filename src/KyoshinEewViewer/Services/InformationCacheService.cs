using KyoshinEewViewer.Core.Models.Events;
using LiteDB;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services
{
	public class InformationCacheService
	{
		private static InformationCacheService? _default;
		public static InformationCacheService Default => _default ??= new InformationCacheService();

		private LiteDatabase CacheDatabase { get; }
		private ILiteCollection<InformationCacheModel> CacheTable { get; }

		private ILogger Logger { get; }

		public InformationCacheService()
		{
			Logger = LoggingService.CreateLogger(this);

			CacheDatabase = new LiteDatabase("cache.db");
			CacheTable = CacheDatabase.GetCollection<InformationCacheModel>();
			CacheTable.EnsureIndex(x => x.Key, true);
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
	}

	public record InformationCacheModel(string Key, string Title, DateTime ArrivalTime, byte[] Body);
}
