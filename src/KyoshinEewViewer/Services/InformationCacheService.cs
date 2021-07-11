using KyoshinEewViewer.Core.Models.Events;
using LiteDB;
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
		private ILiteCollection<JmaInformationHeaderModel> InformationTable { get; }

		public InformationCacheService()
		{
			CacheDatabase = new LiteDatabase("cache.db");
			CacheTable = CacheDatabase.GetCollection<InformationCacheModel>();
			CacheTable.EnsureIndex(x => x.Key, true);
			InformationTable = CacheDatabase.GetCollection<JmaInformationHeaderModel>();
			InformationTable.EnsureIndex(x => x.Url, true);
			MessageBus.Current.Listen<ApplicationClosing>().Subscribe(x => CacheDatabase.Dispose());

			CleanupCache().ConfigureAwait(false);
		}


		/// <summary>
		/// URLを元にキャッシュされたstreamを取得する
		/// </summary>
		public bool TryGetKey(string url, out string key)
		{
			var information = InformationTable.FindOne(i => i.Url == url);
			if (information == null)
			{
#pragma warning disable CS8625 // falseなので普通にnullを代入する
				key = null;
				return false;
#pragma warning restore CS8625
			}
			key = information.Key;
			return true;
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
			var memStream = new MemoryStream(cache.Body);
			stream = new GZipStream(memStream, CompressionMode.Decompress);
			return true;
		}

		public async Task<(string, Stream)> TryGetOrFetchContentFromUrlAsync(string url, Func<Task<(string title, DateTime arrivalTime, Stream body)>> fetcher)
		{
			// URLにマッチするkeyを探す
			var isKeyFound = TryGetKey(url, out var key);
			if (isKeyFound)
				// 見つけた場合、探す
				if (TryGetContent(key, out var stream))
					return (key, stream);

			var (title, arrivalTime, body) = await fetcher();
			var (hash, compressedBytes) = CompressAndCalcHash(body);

			if (!isKeyFound)
			{
				InformationTable.Insert(new JmaInformationHeaderModel(
					url,
					title,
					arrivalTime,
					hash));
			}
			CacheTable.Insert(new InformationCacheModel(
				hash,
				title,
				arrivalTime,
				compressedBytes));
			CacheDatabase.Commit();
			_ = CleanupCache().ConfigureAwait(false);
			body.Seek(0, SeekOrigin.Begin);
			return (hash, body);
		}
		public async Task<Stream> TryGetOrFetchContentAsync(string key, Func<Task<(string title, DateTime arrivalTime, Stream body)>> fetcher)
		{
			if (TryGetContent(key, out var stream))
				return stream;

			var (title, arrivalTime, body) = await fetcher();
			var (hash, compressedBytes) = CompressAndCalcHash(body);

			CacheTable.Insert(new InformationCacheModel(
				hash,
				title,
				arrivalTime,
				compressedBytes));
			CacheDatabase.Commit();
			_ = CleanupCache().ConfigureAwait(false);
			body.Seek(0, SeekOrigin.Begin);
			return body;
		}

		private static (string key, byte[] compressedBytes) CompressAndCalcHash(Stream body)
		{
			using var outStream = new MemoryStream();
			using (var compressStream = new GZipStream(outStream, CompressionLevel.Optimal))
				body.CopyTo(compressStream);
			var compressedBytes = outStream.ToArray();
			var hash = new SHA384Managed().ComputeHash(compressedBytes);

			return (string.Join("", hash.Select(r => r.ToString("x2"))), compressedBytes);
		}

		private Task CleanupCache()
			=> Task.Run(() =>
			{
				CacheDatabase.BeginTrans();
				// 2週間以上経過したものを削除
				InformationTable.DeleteMany(c => c.ArrivalTime < DateTime.Now.AddDays(-14));
				CacheTable.DeleteMany(c => c.ArrivalTime < DateTime.Now.AddDays(-14));
				CacheDatabase.Commit();
			});
	}

	public record JmaInformationHeaderModel(string Url, string Title, DateTime ArrivalTime, string Key);
	public record InformationCacheModel(string Key, string Title, DateTime ArrivalTime, byte[] Body);
}
