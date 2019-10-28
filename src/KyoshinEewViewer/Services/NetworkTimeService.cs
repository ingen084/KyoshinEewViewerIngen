using KyoshinMonitorLib;
using Prism.Events;
using System;
using System.Diagnostics;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services
{
	public class NetworkTimeService
	{
		private Timer NtpTimer { get; }
		private ConfigurationService ConfigService { get; }
		private LoggerService Logger { get; }

		public NetworkTimeService(ConfigurationService configService, LoggerService logger, IEventAggregator aggregator)
		{
			ConfigService = configService ?? throw new ArgumentNullException(nameof(configService));

			ConfigService.Configuration.ConfigurationUpdated += n =>
			{
				switch (n)
				{
					case nameof(ConfigService.Configuration.UseHttpNetworkTime):
						if (ConfigService.Configuration.UseHttpNetworkTime)
						{
							if (!ConfigService.Configuration.NetworkTimeSyncAddress.StartsWith("http"))
								ConfigService.Configuration.NetworkTimeSyncAddress = "http://ntp-a1.nict.go.jp/cgi-bin/jst";
						}
						else
						{
							if (ConfigService.Configuration.NetworkTimeSyncAddress.StartsWith("http"))
								ConfigService.Configuration.NetworkTimeSyncAddress = "ntp.nict.jp";
						}
						break;
				}
			};

			Logger = logger;
			NtpTimer = new Timer(async s =>
			{
				//TODO 分離する
				GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
				Logger.Info("GC Before: " + GC.GetTotalMemory(false));
				GC.Collect();
				Logger.Info("GC After: " + GC.GetTotalMemory(false));

				var time = await GetNowTimeAsync();
				if (time != null)
					aggregator.GetEvent<Events.TimeSynced>().Publish(time.Value);
			}, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10));
		}

		public async Task<DateTime?> GetNowTimeAsync()
		{
			try
			{
				if (!ConfigService.Configuration.EnableNetworkTimeSync)
					return DateTime.Now;
				if (ConfigService.Configuration.UseHttpNetworkTime)
					return await NtpAssistance.GetNetworkTimeWithHttp(ConfigService.Configuration.NetworkTimeSyncAddress);
				return await NtpAssistance.GetNetworkTimeWithNtp(ConfigService.Configuration.NetworkTimeSyncAddress);
			}
			catch (Exception ex)
			{
				Logger.OnWarningMessageUpdated($"時刻同期に失敗しました。");
				Logger.Warning("時刻同期に失敗\n" + ex);
			}
			return null;
		}
	}
}