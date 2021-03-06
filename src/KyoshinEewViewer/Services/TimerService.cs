﻿using KyoshinEewViewer.Models.Events;
using KyoshinMonitorLib;
using KyoshinMonitorLib.Timers;
using Microsoft.Win32;
using Prism.Events;
using System;
using System.Net;
using System.Net.Sockets;
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

			Logger = logger;
			NtpTimer = new Timer(s =>
			{
				//TODO 分離する
				GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
				Logger.Info("LOH GC Before: " + GC.GetTotalMemory(false));
				GC.Collect();
				Logger.Info("LOH GC After: " + GC.GetTotalMemory(false));

				var nullableTime = GetNowTime();
				if (nullableTime is DateTime time)
				{
					Logger.Info($"時刻同期を行いました {time:yyyy/MM/dd HH:mm:ss.fff}");
					MainTimer.UpdateTime(time);
					aggregator.GetEvent<NetworkTimeSynced>().Publish(time);
				}
			}, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10));

			MainTimer = new SecondBasedTimer()
			{
				Offset = TimeSpan.Zero,//TimeSpan.FromMilliseconds(ConfigService.Configuration.Timer.Offset),
				Accuracy = TimeSpan.FromMilliseconds(100),
			};

			DelayedTimeElapsedEvent = aggregator.GetEvent<DelayedTimeElapsed>();
			DelayedTimer = new Timer(s =>
			{
				System.Diagnostics.Debug.WriteLine("dt: " + DelayedTime);

				if (IsDelayedTimerRunning)
					return;

				IsDelayedTimerRunning = true;
				DelayedTimeElapsedEvent.Publish(DelayedTime);
				IsDelayedTimerRunning = false;
			}, null, Timeout.Infinite, Timeout.Infinite);

			TimeElapsedEvent = aggregator.GetEvent<TimeElapsed>();
			MainTimer.Elapsed += t =>
			{
				System.Diagnostics.Debug.WriteLine("mt: " + t);

				var delay = TimeSpan.FromMilliseconds(ConfigService.Configuration.Timer.Offset);
				DelayedTime = t.AddSeconds(-Math.Floor(delay.TotalSeconds));
				DelayedTimer.Change(TimeSpan.FromSeconds(delay.TotalSeconds % 1), Timeout.InfiniteTimeSpan);

				TimeElapsedEvent.Publish(t);
				return Task.CompletedTask;
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
							var nTime = GetNowTime(true);
							if (nTime is DateTime time)
							{
								Logger.Info($"スリープ復帰時の時刻同期を行いました {time:yyyy/MM/dd HH:mm:ss.fff}");
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

		public void StartMainTimer()
		{
			Logger.Info("初回の時刻同期･メインタイマーを開始します。");
			var time = GetNowTime() ?? DateTime.UtcNow.AddHours(9);
			MainTimer.Start(time);
			Logger.Info("メインタイマーを開始しました。");
		}

		public  DateTime? GetNowTime(bool suppressWarning = false)
		{
			try
			{
				if (!ConfigService.Configuration.NetworkTime.Enable)
					return DateTime.UtcNow.AddHours(9);

				DateTime? time = null;
				var count = 0;
				while (true)
				{
					count++;
					time = GetNetworkTimeWithNtp(ConfigService.Configuration.NetworkTime.Address);
					if (time != null)
					{
						Logger.Info($"時刻同期結果: {time:yyyy/MM/dd HH:mm:ss.fff}");
						return time;
					}
					if (count >= 10)
						return null;
				}
			}
			catch (Exception ex)
			{
				if (!suppressWarning)
					Logger.OnWarningMessageUpdated($"時刻同期に失敗しました。");
				Logger.Warning("時刻同期に失敗\n" + ex);
			}
			return null;
		}

		/// <summary>
		/// Ntp通信を使用してネットワーク上から時刻を取得します。
		/// </summary>
		/// <param name="hostName">ホスト名</param>
		/// <param name="port">ポート番号 通常はデフォルトのままで構いません。</param>
		/// <param name="timeout">タイムアウト時間(ミリ秒)</param>
		/// <returns>取得された時刻 取得に失敗した場合はnullが返されます。</returns>
		public DateTime? GetNetworkTimeWithNtp(string hostName = "ntp.nict.jp", ushort port = 123, int timeout = 200)
		{
			try
			{
				// RFC 2030準拠
				var ntpData = new byte[48];

				//特に使用しません
				ntpData[0] = 0b00_100_011;//うるう秒指定子 = 0 (警告なし), バージョン = 4 (SNTP), Mode = 3 (クライアント)

				DateTime sendedTime, recivedTime;
				sendedTime = recivedTime = DateTime.Now;

				if (!IPAddress.TryParse(hostName, out var addr))
				{
					var addresses = Dns.GetHostEntry(hostName).AddressList;
					addr = addresses[new Random().Next(addresses.Length)];
				}

				var endPoint = new IPEndPoint(addr, port);
				using (var socket = new Socket(endPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp))
				{
					socket.Connect(endPoint);
					socket.ReceiveTimeout = timeout;

					socket.Send(ntpData);
					sendedTime = DateTime.Now;

					socket.Receive(ntpData);
					recivedTime = DateTime.Now;
				}

				//受信時刻=32 送信時刻=40
				var serverReceivedTime = ToTime(ntpData, 32);
				var serverSendedTime = ToTime(ntpData, 40);

				// (送信から受信までの時間 - 鯖側での受信から送信までの時間) / 2
				var delta = TimeSpan.FromTicks((recivedTime.Ticks - sendedTime.Ticks - (serverSendedTime.Ticks - serverReceivedTime.Ticks)) / 2);
				Logger.Debug("ntp delta: " + delta);
				return serverSendedTime + delta;
			}
			catch (SocketException ex)
			{
				Logger.Debug("socket exception: " + ex);
				return null;
			}
		}

		private static DateTime ToTime(byte[] bytes, ushort offset)
		{
			ulong intPart = SwapEndianness(BitConverter.ToUInt32(bytes, offset));
			ulong fractPart = SwapEndianness(BitConverter.ToUInt32(bytes, offset + 4));

			var milliseconds = (intPart * 1000) + (fractPart * 1000 / 0x100000000L);

			//時間生成
			return new DateTime(1900, 1, 1, 9, 0, 0).AddMilliseconds((long)milliseconds);
		}

		//ビット列を逆にする stackoverflow.com/a/3294698/162671
		internal static uint SwapEndianness(ulong x)
			=> (uint)(((x & 0x000000ff) << 24) +
					  ((x & 0x0000ff00) << 8) +
					  ((x & 0x00ff0000) >> 8) +
					  ((x & 0xff000000) >> 24));
	}
}