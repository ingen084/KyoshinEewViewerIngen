using KyoshinEewViewer.Models.Events;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Services
{
	public class EewControlService
	{
		private Dictionary<string, Models.Eew> EewCache { get; } = new();
		/// <summary>
		/// 発生中のEEWが存在するか
		/// </summary>
		public bool Found => EewCache.Count > 0;

		private EewUpdated EewUpdatedEvent { get; }
		private LoggerService Logger { get; }

		public EewControlService(LoggerService logger, IEventAggregator aggregator)
		{
			EewUpdatedEvent = aggregator.GetEvent<EewUpdated>();
			Logger = logger;
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

		internal bool UpdateOrRefreshEewInternal(Models.Eew eew, DateTime updatedTime)
		{
			var isUpdated = false;

			// 最終アップデートから1分経過していれば削除
			var removes = new List<string>();
			foreach (var e in EewCache)
			{
				var diff = updatedTime - e.Value.UpdatedTime;
				if (diff >= TimeSpan.FromMinutes(1)) {
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
				foreach(var e in EewCache.Values.Where(e => e.Source == Models.EewSource.NIED && !e.IsFinal && !e.IsCancelled && e.UpdatedTime < updatedTime))
				{
					Logger.Info("NIEDからのリクエストでEEWをキャンセル扱いにしました: " + EewCache.First().Value.Id);
					e.IsCancelled = true;
					e.UpdatedTime = updatedTime;
					isUpdated = true;
				}
				return isUpdated;
			}

			// 新しいデータ or 元のソースが強震モニタであれば置き換え
			if (!EewCache.TryGetValue(eew.Id, out var cEew)
				 || eew.Count > cEew.Count
				 || (cEew.Source == Models.EewSource.NIED && eew.Source != Models.EewSource.NIED))
			{
				Logger.Info($"EEWを更新しました source:{eew.Source} id:{eew.Id} cound:{eew.Count} ");
				EewCache[eew.Id] = eew;
				isUpdated = true;
			}
			// 置き換え対象ではなく単にupdatetimeを更新する場合
			else if (EewCache.TryGetValue(eew.Id, out var cEew2))
				eew.UpdatedTime = cEew2.UpdatedTime;

			return isUpdated;
		}
	}
}
