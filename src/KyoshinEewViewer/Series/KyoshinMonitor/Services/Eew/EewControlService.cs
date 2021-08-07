using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.Services;
using KyoshinMonitorLib;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew
{
	public class EewControlService
	{
		private static EewControlService? _default;
		public static EewControlService Default => _default ??= new();

		private ILogger Logger { get; }

		private Dictionary<string, Core.Models.Eew> EewCache { get; } = new();
		/// <summary>
		/// 発生中のEEWが存在するか
		/// </summary>
		public bool Found => EewCache.Count > 0;

		private DateTime CurrentTime { get; set; } = DateTime.Now;

		public EewControlService()
		{
			MessageBus.Current.Listen<TimerElapsed>().Subscribe(t => CurrentTime = t.Time);
			Logger = LoggingService.CreateLogger(this);
		}

		/// <summary>
		/// 複数のソースで発生したEEWを統合して管理する
		/// </summary>
		/// <param name="eew">発生したEEW / キャッシュのクリアチェックのみを行う場合はnull</param>
		/// <param name="updatedTime">そのEEWを受信した時刻</param>
		public void UpdateOrRefreshEew(Core.Models.Eew? eew, DateTime updatedTime)
		{
			if (UpdateOrRefreshEewInternal(eew, updatedTime))
				MessageBus.Current.SendMessage(new EewUpdated(updatedTime, EewCache.Values.ToArray()));
		}

		private bool UpdateOrRefreshEewInternal(Core.Models.Eew? eew, DateTime updatedTime)
		{
			var isUpdated = false;

			// 最終アップデートから1分経過していれば削除
			var removes = new List<string>();
			foreach (var e in EewCache)
			{
				var diff = updatedTime - e.Value.UpdatedTime;
				// 1分前であるか、NIEDかつオフセット以上に遅延している場合削除
				if (diff >= TimeSpan.FromMinutes(1) ||
					(e.Value.Source == EewSource.NIED && (CurrentTime - TimeSpan.FromSeconds(-ConfigurationService.Default.Timer.TimeshiftSeconds) - e.Value.UpdatedTime) < TimeSpan.FromMilliseconds(-ConfigurationService.Default.Timer.Offset)))
				{
					Logger.LogInformation("EEWキャッシュ削除: " + e.Value.Id);
					removes.Add(e.Key);
				}
			}
			foreach (var r in removes)
			{
				EewCache.Remove(r);
				isUpdated = true;
			}

			// 更新されたEEWが存在しなければそのまま終了
			if (string.IsNullOrWhiteSpace(eew?.Id))
			{
				// EEWが存在しない場合NIEDの過去のEEWはすべてキャンセル扱いとする
				foreach (var e in EewCache.Values.Where(e => e.Source == EewSource.NIED && !e.IsFinal && !e.IsCancelled && e.UpdatedTime < updatedTime))
				{
					Logger.LogInformation("NIEDからのリクエストでEEWをキャンセル扱いにしました: " + EewCache.First().Value.Id);
					e.IsCancelled = true;
					e.UpdatedTime = updatedTime;
					isUpdated = true;
				}
				return isUpdated;
			}

			// 新しいデータ or 元のソースがSNPであれば置き換え
			if (!EewCache.TryGetValue(eew.Id, out var cEew)
				 || eew.Count > cEew.Count
				 || (eew.Count >= cEew.Count && cEew.Source == EewSource.SignalNowProfessional))
			{
				if (ConfigurationService.Default.Notification.EewReceived)
					NotificationService.Default.Notify(eew.Title, $"最大{eew.Intensity.ToLongString()}/{eew.PlaceString}/M{eew.Magnitude:0.0}/{eew.Depth}km\n{eew.Source}");
				Logger.LogInformation($"EEWを更新しました source:{eew.Source} id:{eew.Id} count:{eew.Count} isFinal:{eew.IsFinal} updatedTime:{eew.UpdatedTime:yyyy/MM/dd HH:mm:ss.fff} ");
				EewCache[eew.Id] = eew;
				isUpdated = true;
			}
			// 置き換え対象ではなく単にupdatetimeを更新する場合
			else if (EewCache.TryGetValue(eew.Id, out var cEew2))
				cEew2.UpdatedTime = eew.UpdatedTime;

			return isUpdated;
		}
	}
}
