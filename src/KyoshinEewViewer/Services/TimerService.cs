using KyoshinMonitorLib;
using KyoshinMonitorLib.Timers;
using Prism.Events;
using System;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services
{
	public class TimerService
	{
		private readonly Timer _ntpTimer;

		private SecondBasedTimer MainTimer { get; }
		private Timer UpdateOffsetTimer { get; }
		private ConfigurationService ConfigService { get; }
		private LoggerService Logger { get; }
		private Events.TimeElapsed TimeElapsedEvent { get; }

		public event Func<DateTime, Task> MainTimerElapsed;

		public TimerService(ConfigurationService configService, LoggerService logger, IEventAggregator aggregator)
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

					case nameof(ConfigService.Configuration.Offset):
						UpdateOffsetTimer.Change(1000, Timeout.Infinite);
						break;
				}
			};

			Logger = logger;
			_ntpTimer = new Timer(async s =>
			{
				//TODO 分離する
				GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
				Logger.Info("LOH GC Before: " + GC.GetTotalMemory(false));
				GC.Collect();
				Logger.Info("LOH GC After: " + GC.GetTotalMemory(false));

				var nullableTime = await GetNowTimeAsync();
				if (nullableTime is DateTime time)
				{
					MainTimer.UpdateTime(time);
					aggregator.GetEvent<Events.NetworkTimeSynced>().Publish(time);
				}
			}, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10));

			MainTimer = new SecondBasedTimer()
			{
				Offset = TimeSpan.FromMilliseconds(ConfigService.Configuration.Offset),
			};
			UpdateOffsetTimer = new Timer(s => MainTimer.Offset = TimeSpan.FromMilliseconds(ConfigService.Configuration.Offset));

			TimeElapsedEvent = aggregator.GetEvent<Events.TimeElapsed>();
			MainTimer.Elapsed += async t => {
				await Task.Run(() => TimeElapsedEvent.Publish(t));
				if (MainTimerElapsed != null)
					await MainTimerElapsed.Invoke(t);
			};
		}

		public async Task StartMainTimerAsync()
		{
			Logger.Info("初回の時刻同期･メインタイマーを開始します。");
			MainTimer.Start(await GetNowTimeAsync() ?? DateTime.Now);
			Logger.Info("メインタイマーを開始しました。");
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