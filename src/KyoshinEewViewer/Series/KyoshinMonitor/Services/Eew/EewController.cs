using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Services;
using KyoshinMonitorLib;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;

public class EewController
{
	private ILogger Logger { get; }
	private NotificationService? NotificationService { get; }

	private Dictionary<string, IEew> EewCache { get; } = new();
	/// <summary>
	/// 発生中のEEWが存在するか
	/// </summary>
	public bool Found => EewCache.Count > 0;

	private Sound EewReceivedSound { get; }
	private Sound EewBeginReceivedSound { get; }
	private Sound EewFinalReceivedSound { get; }
	private Sound EewCanceledSound { get; }

	private DateTime CurrentTime { get; set; } = DateTime.Now;

	public event Action<(DateTime time, IEew[] eews)>? EewUpdated;

	public EewController(SoundCategory category, NotificationService? notificationService)
	{
		Logger = LoggingService.CreateLogger(this);
		NotificationService = notificationService;
		TimerService.Default.TimerElapsed += t => CurrentTime = t;

		EewReceivedSound = SoundPlayerService.RegisterSound(category, "EewReceived", "緊急地震速報受信", "{int}: 最大震度 [？,0,1,...,6-,6+,7]", new() { { "int", "4" }, });
		EewBeginReceivedSound = SoundPlayerService.RegisterSound(category, "EewBeginReceived", "緊急地震速報受信(初回)", "{int}: 最大震度 [-,0,1,...,6-,6+,7]", new() { { "int", "5+" }, });
		EewFinalReceivedSound = SoundPlayerService.RegisterSound(category, "EewFinalReceived", "緊急地震速報受信(最終)", "{int}: 最大震度 [-,0,1,...,6-,6+,7]", new() { { "int", "-" }, });
		EewCanceledSound = SoundPlayerService.RegisterSound(category, "EewCanceled", "緊急地震速報受信(キャンセル)");
	}

	/// <summary>
	/// 複数のソースで発生したEEWを統合して管理する
	/// </summary>
	/// <param name="eew">発生したEEW / キャッシュのクリアチェックのみを行う場合はnull</param>
	/// <param name="updatedTime">そのEEWを受信した時刻</param>
	public void UpdateOrRefreshEew(IEew? eew, DateTime updatedTime)
	{
		if (UpdateOrRefreshEewInternal(eew, updatedTime))
			EewUpdated?.Invoke((updatedTime, EewCache.Values.ToArray()));
	}

	private bool UpdateOrRefreshEewInternal(IEew? eew, DateTime updatedTime)
	{
		var isUpdated = false;

		// 最終アップデートから1分経過していれば削除
		var removes = new List<string>();
		foreach (var e in EewCache)
		{
			var diff = updatedTime - e.Value.UpdatedTime;
			// 1分前であるか、NIEDかつオフセット以上に遅延している場合削除
			if (diff >= TimeSpan.FromMinutes(1) ||
				(e.Value is KyoshinMonitorEew && (CurrentTime - TimeSpan.FromSeconds(-ConfigurationService.Current.Timer.TimeshiftSeconds) - e.Value.UpdatedTime) < TimeSpan.FromMilliseconds(-ConfigurationService.Current.Timer.Offset)))
			{
				Logger.LogInformation("EEWキャッシュ削除: {Id}", e.Value.Id);
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
			foreach (var e in EewCache.Values.Where(e => e is KyoshinMonitorEew && !e.IsFinal && !e.IsCancelled && e.UpdatedTime < updatedTime))
			{
				Logger.LogInformation("NIEDからのリクエストでEEWをキャンセル扱いにしました: {Id}", EewCache.First().Value.Id);
				if(e is KyoshinMonitorEew kme)
					kme.IsCancelled = true;
				e.UpdatedTime = updatedTime;
				isUpdated = true;

				if (!EewCanceledSound.Play())
					EewReceivedSound.Play(new() { { "int", "？" } });
			}
			return isUpdated;
		}

		// 詳細を表示しない設定かつ1点での場合処理しない 警報･キャンセルのときのみ処理する
		if (!eew.IsCancelled && !eew.IsWarning && !ConfigurationService.Current.Eew.ShowDetails && eew.LocationAccuracy == 1 && eew.DepthAccuracy == 1)
			return false;

		// 新しいデータ or Priority の高い順番で置き換える
		if (!EewCache.TryGetValue(eew.Id, out var cEew)
			 || eew.Count > cEew.Count
			 || (eew.Count == cEew.Count && eew.IsCancelled)
			 || (eew.Count == cEew.Count && eew.Priority > cEew.Priority))
		{
			// 報数が同じ場合精度情報を移植する
			if (cEew != null && !eew.IsAccuracyFound && eew.Count == cEew.Count && cEew.IsAccuracyFound)
			{
				eew.LocationAccuracy = cEew.LocationAccuracy;
				eew.DepthAccuracy = cEew.DepthAccuracy;
				eew.MagnitudeAccuracy = cEew.MagnitudeAccuracy;
			}

			var intStr = eew.Intensity.ToShortString().Replace('*', '-');

			// 音声の再生
			if (EewCache.TryGetValue(eew.Id, out var cEew2))
			{
				if (eew.IsFinal)
				{
					if (!cEew2.IsFinal && !EewFinalReceivedSound.Play(new() { { "int", intStr } }))
						EewReceivedSound.Play(new() { { "int", intStr } });
				}
				else if (eew.IsCancelled)
				{
					if (!cEew2.IsCancelled && !EewCanceledSound.Play())
						EewReceivedSound.Play(new() { { "int", "？" } });
				}
				else if (eew.Count > cEew2.Count)
					EewReceivedSound.Play(new() { { "int", intStr } });
			}
			else if (!EewBeginReceivedSound.Play(new() { { "int", intStr } }))
				EewReceivedSound.Play(new() { { "int", intStr } });

			if (ConfigurationService.Current.Notification.EewReceived && ConfigurationService.Current.Timer.TimeshiftSeconds == 0)
			{
				if (eew.IsCancelled)
					NotificationService?.Notify($"緊急地震速報({eew.Count:00}報)", eew.IsTrueCancelled ? "キャンセルされました" : "キャンセルされたか、受信範囲外になりました");
				else
					NotificationService?.Notify($"緊急地震速報({eew.Count:00}報)", $"最大{eew.Intensity.ToLongString()}/{eew.Place}/M{eew.Magnitude:0.0}/{eew.Depth}km\n{eew.SourceDisplay}");
			}

			EventHookService.Run("EEW_RECEIVED", new()
			{
				{ "EEW_SOURCE", eew.SourceDisplay },
				{ "EEW_EVENT_ID", eew.Id },
				{ "EEW_COUNT", eew.Count.ToString() },
				{ "EEW_PLACE", eew.Place ?? "" },
				{ "EEW_INTENSITY", eew.Intensity.ToShortString() },
				{ "EEW_IS_FINAL", eew.IsFinal.ToString() },
				{ "EEW_IS_CANCEL", eew.IsCancelled.ToString() },
			}).ConfigureAwait(false);

			Logger.LogInformation("EEWを更新しました source:{Source} id:{Id} count:{Count} isFinal:{IsFinal} isCanceled:{IsCanceled} updatedTime:{UpdatedTime:yyyy/MM/dd HH:mm:ss.fff}", eew.SourceDisplay, eew.Id, eew.Count, eew.IsFinal, eew.IsCancelled, eew.UpdatedTime);
			EewCache[eew.Id] = eew;
			isUpdated = true;
		}
		// 置き換え対象ではなく単にupdatetimeを更新する場合
		else if (EewCache.TryGetValue(eew.Id, out var cEew2))
			cEew2.UpdatedTime = eew.UpdatedTime;

		return isUpdated;
	}
}
