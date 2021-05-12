using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.Services.InformationProvider;
using LiteDB;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services
{
	public class InformationProviderService
	{
		private static InformationProviderService? _default;
		public static InformationProviderService Default => _default ??= new InformationProviderService();

		private LiteDatabase CacheDatabase { get; }
		private ILiteCollection<InformationCacheModel> CacheTable { get; }

		public event Action<InformationHeader>? NewDataArrived;

		//public DmdataProvider Dmdata { get; private set; }
		public JmaXmlPullProvider Jma { get; private set; }


		public InformationProviderService()
		{
			CacheDatabase = new LiteDatabase("cache.db");
			CacheTable = CacheDatabase.GetCollection<InformationCacheModel>();
			MessageBus.Current.Listen<ApplicationClosing>().Subscribe(x => CacheDatabase.Dispose());
			CacheTable.EnsureIndex(x => x.Key, true);

			CleanupCache().ConfigureAwait(false);

			Jma = new JmaXmlPullProvider();
			Jma.NewFeedArrived += f =>
			{
				NewDataArrived?.Invoke(f);
			};
		}

		/// <summary>
		/// 情報の取得･監視を開始し、過去に存在する指定した情報のInformationHeaderを取得する
		/// </summary>
		/// <param name="matchTitles">指定するTitle(完全一致)</param>
		/// <param name="matchTypes">指定するType(完全一致)</param>
		/// <returns></returns>
		public async Task<IEnumerable<InformationHeader>> StartAndGetInformationHistoryAsync(string[] matchTitles, string[] matchTypes)
		{
			await Jma.EnableAsync();
			return Jma.GetInformationHistory(matchTitles);
		}
		/// <summary>
		/// InformationHeaderに結び付けられた情報のボディを取得する
		/// </summary>
		/// <param name="info">該当のInformationHeader</param>
		/// <returns>ボディ かならずDisposeしてください</returns>
		public async Task<Stream> FetchContentAsync(InformationHeader info)
		{
			switch (info.Source)
			{
				case InformationSource.Dmdata:
					throw new NotImplementedException();
				case InformationSource.Jma:
					{
						var data = CacheTable.FindOne(i => i.Key == info.Key);
						if (data != null)
						{
							var memStream = new MemoryStream(data.Body);
							var stream = new GZipStream(memStream, CompressionMode.Decompress);
							return stream;
						}
						var body = await Jma.FetchAsync(info.Url ?? throw new ArgumentNullException(nameof(info)));

						using (var outStream = new MemoryStream())
						{
							using (var stream = new GZipStream(outStream, CompressionLevel.Optimal))
								body.CopyTo(stream);
							CacheTable.Insert(new InformationCacheModel(
								InformationSource.Jma,
								info.Key,
								info.Title,
								info.ArrivalTime,
								outStream.ToArray()));
						}
						CacheDatabase.Commit();
						_ = CleanupCache().ConfigureAwait(false);
						body.Seek(0, SeekOrigin.Begin);
						return body;
					}
				default:
					throw new ArgumentOutOfRangeException(nameof(info));
			}
		}

		private Task CleanupCache()
			=> Task.Run(() =>
			{
				CacheDatabase.BeginTrans();
				// 2週間以上経過したものを削除
				CacheTable.DeleteMany(c => c.ArrivalTime < DateTime.Now.AddDays(-14));
				CacheDatabase.Commit();
			});
	}
	public record InformationHeader(InformationSource Source, string Key, string Title, DateTime ArrivalTime, string? Url);

	public record InformationCacheModel(InformationSource Source, string Key, string Title, DateTime ArrivalTime, byte[] Body);
	public enum InformationSource
	{
		Jma,
		Dmdata,
	}
}
