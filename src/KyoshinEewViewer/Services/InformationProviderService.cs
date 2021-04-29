using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.Services.InformationProvider;
using LiteDB;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services
{
	public class InformationProviderService
	{
		private static InformationProviderService? _default;
		public static InformationProviderService Default => _default ??= new InformationProviderService();

		private LiteDatabase CacheDatabase { get; }
		private ILiteCollection<InformationCacheModel> CacheTable { get; }

		public DmdataProvider Dmdata { get; private set; }
		public JmaXmlPullProvider Jma { get; private set; }


		public InformationProviderService()
		{
			CacheDatabase = new LiteDatabase("cache.db");
			CacheTable = CacheDatabase.GetCollection<InformationCacheModel>();
			MessageBus.Current.Listen<ApplicationClosing>().Subscribe(x => CacheDatabase.Dispose());

			CacheTable.EnsureIndex(x => x.Key, true);
		}
	}

	public record InformationCacheModel(string Key, InformationSource Source, DateTime ArrivalTime, string Body);
	public enum InformationSource
	{
		Jma,
		Dmdata,
	}
}
