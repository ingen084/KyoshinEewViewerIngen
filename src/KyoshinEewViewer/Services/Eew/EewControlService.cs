using KyoshinEewViewer.Models.Events;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Services.Eew
{
	public class EewControlService
	{
		private Dictionary<string, Models.Eew> EewCache { get; } = new();
		/// <summary>
		/// 発生中のEEWが存在するか
		/// </summary>
		public bool Found => EewCache.Count > 0;

		private DateTime CurrentTime { get; set; } = DateTime.Now;
		private EewUpdated EewUpdatedEvent { get; }
		private LoggerService Logger { get; }
		private ConfigurationService ConfigurationService { get; }

		public EewControlService(LoggerService logger, ConfigurationService configurationService, IEventAggregator aggregator)
		{
			EewUpdatedEvent = aggregator.GetEvent<EewUpdated>();
			aggregator.GetEvent<TimeElapsed>().Subscribe(t => CurrentTime = t);
			Logger = logger;
			ConfigurationService = configurationService;
		}

		/// <summary>
		/// 複数のソースで発生したEEWを統合して管理する
		/// </summary>
		/// <param name="eew">発生したEEW / キャッシュのクリアチェックのみを行う場合はnull</param>
		/// <param name="updatedTime">そのEEWを受信した時刻</param>
		public void UpdateOrRefreshEew(Models.Eew eew, DateTime updatedTime)
		{
			if (UpdateOrRefreshEewInternal(eew, updatedTime))
				EewUpdatedEvent.Publish(new EewUpdated
				{
					Eews = EewCache.Values.ToArray(),
					Time = updatedTime
				});
		}

		private bool UpdateOrRefreshEewInternal(Models.Eew eew, DateTime updatedTime)
		{
			var isUpdated = false;

			// 最終アップデートから1分経過していれば削除
			var removes = new List<string>();
			foreach (var e in EewCache)
			{
				var diff = updatedTime - e.Value.UpdatedTime;
				// 1分前であるか、NIEDかつオフセット以上に遅延している場合削除
				if (diff >= TimeSpan.FromMinutes(1) ||
					(e.Value.Source == Models.EewSource.NIED && (CurrentTime - TimeSpan.FromSeconds(-ConfigurationService.Configuration.Timer.TimeshiftSeconds) - e.Value.UpdatedTime) < TimeSpan.FromMilliseconds(-ConfigurationService.Configuration.Timer.Offset)))
				{
					Logger.Info("EEWキャッシュ削除: " + e.Value.Id);
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
				foreach (var e in EewCache.Values.Where(e => e.Source == Models.EewSource.NIED && !e.IsFinal && !e.IsCancelled && e.UpdatedTime < updatedTime))
				{
					Logger.Info("NIEDからのリクエストでEEWをキャンセル扱いにしました: " + EewCache.First().Value.Id);
					e.IsCancelled = true;
					e.UpdatedTime = updatedTime;
					isUpdated = true;
				}
				return isUpdated;
			}

			// 新しいデータ or 元のソースがSNPであれば置き換え
			if (!EewCache.TryGetValue(eew.Id, out var cEew)
				 || eew.Count > cEew.Count
				 || cEew.Source == Models.EewSource.SignalNowProfessional)
			{
				Logger.Info($"EEWを更新しました source:{eew.Source} id:{eew.Id} cound:{eew.Count} isFinal:{eew.IsFinal} updatedTime:{eew.UpdatedTime:yyyy/MM/dd HH:mm:ss.fff} ");
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
