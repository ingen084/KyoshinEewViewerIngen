using KyoshinEewViewer.Models.Events;
using KyoshinMonitorLib;
using KyoshinMonitorLib.Timers;
using Microsoft.Win32;
using Prism.Events;
using System;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services
{
	public class TimerService
	{
		/// <summary>
		/// 時刻同期･Large Object heapのGCを行うタイマー
		/// </summary>
		private Timer NtpTimer { get; }
		/// <summary>
		/// 正確な日本標準時を刻むだけの
		/// </summary>
		private SecondBasedTimer MainTimer { get; }
		/// <summary>
		/// 遅延タイマーが発行する時刻
		/// </summary>
		private DateTime DelayedTime { get; set; }
		/// <summary>
		/// 遅延タイマー 1秒未満のオフセットを処理する
		/// </summary>
		private Timer DelayedTimer { get; }
		/// <summary>
		/// 遅延タイマーが動作中かどうか
		/// </summary>
		private bool IsDelayedTimerRunning { get; set; }

		private ConfigurationService ConfigService { get; }
		private LoggerService Logger { get; }
		private DelayedTimeElapsed DelayedTimeElapsedEvent { get; }
		private TimeElapsed TimeElapsedEvent { get; }

		public TimerService(ConfigurationService configService, LoggerService logger, IEventAggregator aggregator)
		{
			ConfigService = configService ?? throw new ArgumentNullException(nameof(configService));
			ConfigService.Configuration.NetworkTime.PropertyChanged += (s, e) =>
			{
				switch (e.PropertyName)
				{
					case nameof(ConfigService.Configuration.NetworkTime.UseHttp):
						if (ConfigService.Configuration.NetworkTime.UseHttp)
						{
							if (!ConfigService.Configuration.NetworkTime.Address.StartsWith("http"))
								ConfigService.Configuration.NetworkTime.Address = "http://ntp-a1.nict.go.jp/cgi-bin/jst";
						}
						else
						{
							if (ConfigService.Configuration.NetworkTime.Address.StartsWith("http"))
								ConfigService.Configuration.NetworkTime.Address = "ntp.nict.jp";
						}
						break;
				}
			};

			Logger = logger;
			NtpTimer = new Timer(async s =>
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
					aggregator.GetEvent<NetworkTimeSynced>().Publish(time);
				}
			}, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10));
			
			MainTimer = new SecondBasedTimer()
			{
				//Offset = TimeSpan.FromMilliseconds(ConfigService.Configuration.Timer.Offset),
				Accuracy = TimeSpan.FromMilliseconds(100)
			};

			DelayedTimeElapsedEvent = aggregator.GetEvent<DelayedTimeElapsed>();
			DelayedTimer = new Timer(s =>
			{
				DelayedTimeElapsedEvent.Publish(DelayedTime);
				IsDelayedTimerRunning = false;
			}, null, Timeout.Infinite, Timeout.Infinite);

			TimeElapsedEvent = aggregator.GetEvent<TimeElapsed>();
			MainTimer.Elapsed += async t =>
			{
				if (!IsDelayedTimerRunning)
				{
					IsDelayedTimerRunning = true;
					var delay = TimeSpan.FromMilliseconds(ConfigService.Configuration.Timer.Offset);
					DelayedTime = t.AddSeconds(-Math.Floor(delay.TotalSeconds));
					DelayedTimer.Change(TimeSpan.FromSeconds(delay.TotalSeconds % 1), Timeout.InfiniteTimeSpan);
				}

				await Task.Run(() => TimeElapsedEvent.Publish(t));
			};

			SystemEvents.PowerModeChanged += async (s, e) =>
			{
				switch (e.Mode)
				{
					case PowerModes.Resume:
						Logger.OnWarningMessageUpdated("時刻同期完了までしばらくお待ち下さい。");
						MainTimer.Stop();
						int count = 0;
						while (true)
						{
							var nTime = await GetNowTimeAsync(true);
							if (nTime is DateTime time)
							{
								MainTimer.Start(time);
								return;
							}
							count++;
							if (count >= 10)
							{
								Logger.OnWarningMessageUpdated("時刻同期できなかったため、ローカル時間を使用しました。");
								MainTimer.Start(DateTime.UtcNow.AddHours(9));
								return;
							}
							await Task.Delay(1000);
						}
				}
			};
		}

		public async Task StartMainTimerAsync()
		{
			Logger.Info("初回の時刻同期･メインタイマーを開始します。");
			MainTimer.Start(await GetNowTimeAsync() ?? DateTime.UtcNow.AddHours(9));
			Logger.Info("メインタイマーを開始しました。");
		}

		public async Task<DateTime?> GetNowTimeAsync(bool suppressWarning = false)
		{
			try
			{
				if (!ConfigService.Configuration.NetworkTime.Enable)
					return DateTime.UtcNow.AddHours(9);
				if (ConfigService.Configuration.NetworkTime.UseHttp)
					return await NtpAssistance.GetNetworkTimeWithHttp(ConfigService.Configuration.NetworkTime.Address);
				return await NtpAssistance.GetNetworkTimeWithNtp(ConfigService.Configuration.NetworkTime.Address);
			}
			catch (Exception ex)
			{
				if (!suppressWarning)
					Logger.OnWarningMessageUpdated($"時刻同期に失敗しました。");
				Logger.Warning("時刻同期に失敗\n" + ex);
			}
			return null;
		}
	}
}