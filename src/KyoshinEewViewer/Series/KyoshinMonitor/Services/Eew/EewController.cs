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
	private NotificationService NotificationService { get; }

	private Dictionary<string, Models.Eew> EewCache { get; } = new();
	/// <summary>
	/// 発生中のEEWが存在するか
	/// </summary>
	public bool Found => EewCache.Count > 0;

	private SoundPlayerService.Sound EewReceivedSound { get; }
	private SoundPlayerService.Sound EewBeginReceivedSound { get; }
	private SoundPlayerService.Sound EewFinalReceivedSound { get; }
	private SoundPlayerService.Sound EewCanceledSound { get; }

	private DateTime CurrentTime { get; set; } = DateTime.Now;

	public event Action<(DateTime time, Models.Eew[] eews)>? EewUpdated;

	public EewController(SoundPlayerService.SoundCategory category, NotificationService notificationService)
	{
		Logger = LoggingService.CreateLogger(this);
		NotificationService = notificationService;
		TimerService.Default.TimerElapsed += t => CurrentTime = t;

		EewReceivedSound = SoundPlayerService.RegisterSound(category, "EewReceived", "緊急地震速報受信");
		EewBeginReceivedSound = SoundPlayerService.RegisterSound(category, "EewBeginReceived", "緊急地震速報受信(初回)");
		EewFinalReceivedSound = SoundPlayerService.RegisterSound(category, "EewFinalReceived", "緊急地震速報受信(最終)");
		EewCanceledSound = SoundPlayerService.RegisterSound(category, "EewCanceled", "緊急地震速報受信(キャンセル)");
	}

	/// <summary>
	/// 複数のソースで発生したEEWを統合して管理する
	/// </summary>
	/// <param name="eew">発生したEEW / キャッシュのクリアチェックのみを行う場合はnull</param>
	/// <param name="updatedTime">そのEEWを受信した時刻</param>
	public void UpdateOrRefreshEew(Models.Eew? eew, DateTime updatedTime, bool isTimeShifting)
	{
		if (UpdateOrRefreshEewInternal(eew, updatedTime, isTimeShifting))
			EewUpdated?.Invoke((updatedTime, EewCache.Values.ToArray()));
	}

	private bool UpdateOrRefreshEewInternal(Models.Eew? eew, DateTime updatedTime, bool isTimeShifting)
	{
		var isUpdated = false;

		// 最終アップデートから1分経過していれば削除
		var removes = new List<string>();
		foreach (var e in EewCache)
		{
			var diff = updatedTime - e.Value.UpdatedTime;
			// 1分前であるか、NIEDかつオフセット以上に遅延している場合削除
			if (diff >= TimeSpan.FromMinutes(1) ||
				(e.Value.Source == EewSource.NIED && (CurrentTime - TimeSpan.FromSeconds(-ConfigurationService.Current.Timer.TimeshiftSeconds) - e.Value.UpdatedTime) < TimeSpan.FromMilliseconds(-ConfigurationService.Current.Timer.Offset)))
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
			foreach (var e in EewCache.Values.Where(e => e.Source == EewSource.NIED && !e.IsFinal && !e.IsCancelled && e.UpdatedTime < updatedTime))
			{
				Logger.LogInformation("NIEDからのリクエストでEEWをキャンセル扱いにしました: {Id}", EewCache.First().Value.Id);
				e.IsCancelled = true;
				e.UpdatedTime = updatedTime;
				isUpdated = true;

				if (!EewCanceledSound.Play())
					EewReceivedSound.Play();
			}
			return isUpdated;
		}

		// 新しいデータ or 元のソースがSNPであれば置き換え
		if (!EewCache.TryGetValue(eew.Id, out var cEew)
			 || eew.Count > cEew.Count
			 || (eew.Count >= cEew.Count && cEew.Source == EewSource.SignalNowProfessional))
		{
			// 音声の再生
			if (EewCache.TryGetValue(eew.Id, out var cEew2))
			{
				if (eew.IsFinal)
				{
					if (!cEew2.IsFinal && !EewFinalReceivedSound.Play())
							EewReceivedSound.Play();
				}
				else if (eew.IsCancelled)
				{
					if (!cEew2.IsCancelled && !EewCanceledSound.Play())
							EewReceivedSound.Play();
				}
				else if (eew.Count > cEew2.Count)
					EewReceivedSound.Play();
			}
			else if (!EewBeginReceivedSound.Play())
				EewReceivedSound.Play();

			if (ConfigurationService.Current.Notification.EewReceived && !isTimeShifting)
				NotificationService.Notify(eew.Title, $"最大{eew.Intensity.ToLongString()}/{eew.PlaceString}/M{eew.Magnitude:0.0}/{eew.Depth}km\n{eew.Source}");
			Logger.LogInformation("EEWを更新しました source:{Source} id:{Id} count:{Count} isFinal:{IsFinal} updatedTime:{UpdatedTime:yyyy/MM/dd HH:mm:ss.fff}", eew.Source, eew.Id, eew.Count, eew.IsFinal, eew.UpdatedTime);
			EewCache[eew.Id] = eew;
			isUpdated = true;
		}
		// 置き換え対象ではなく単にupdatetimeを更新する場合
		else if (EewCache.TryGetValue(eew.Id, out var cEew2))
			cEew2.UpdatedTime = eew.UpdatedTime;

		return isUpdated;
	}
}
