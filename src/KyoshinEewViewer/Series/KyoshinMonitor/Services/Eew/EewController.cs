using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Services;
using KyoshinMonitorLib;
using Splat;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;

public class EewController
{
	private ILogger Logger { get; }
	private KyoshinEewViewerConfiguration Config { get; }
	private NotificationService NotificationService { get; }
	private EventHookService EventHook { get; }
	public SoundCategory SoundCategory { get; } = new("Eew", "緊急地震速報");

	private Dictionary<string, IEew> EewCache { get; } = new();
	/// <summary>
	/// 発生中のEEWが存在するか
	/// </summary>
	public bool Found => EewCache.Any(e => e.Value.IsVisible);

	private Sound EewReceivedSound { get; }
	private Sound EewBeginReceivedSound { get; }
	private Sound EewFinalReceivedSound { get; }
	private Sound EewCanceledSound { get; }

	public event Action<(DateTime time, IEew[] eews)>? EewUpdated;

	public EewController(ILogManager logManager, KyoshinEewViewerConfiguration config, TimerService timer, NotificationService notificationService, SoundPlayerService soundPlayer, EventHookService eventHook)
	{
		SplatRegistrations.RegisterLazySingleton<EewController>();

		Logger = logManager.GetLogger<EewController>();
		Config = config;
		NotificationService = notificationService;
		EventHook = eventHook;
		// 古い EEW を消すためのタイマー
		timer.TimerElapsed += t =>
		{
			lock (EewCache)
			{
				var isUpdated = false;
				foreach (var e in EewCache.Values.Where(e => e.IsVisible))
				{
					var diff = t - e.UpdatedTime;
					// 最終orキャンセルから2分経過
					// もしくは3分経過していれば削除
					if (((e.IsFinal || e.IsCancelled) && diff >= TimeSpan.FromMinutes(2)) || diff >= TimeSpan.FromMinutes(3))
					{
						Logger.LogInfo($"EEW終了(期限切れ): {e.Id} {diff.TotalSeconds:0.000}s");
						e.IsVisible = false;
						isUpdated = true;
					}
					else if (e is KyoshinMonitorEew && (t - TimeSpan.FromSeconds(-Config.Timer.TimeshiftSeconds) - e.UpdatedTime) < TimeSpan.FromMilliseconds(-Config.Timer.Offset))
					{
						Logger.LogInfo($"EEW終了(kmoni): {e.Id} {(t - TimeSpan.FromSeconds(-Config.Timer.TimeshiftSeconds) - e.UpdatedTime).TotalSeconds:0.000}s");
						e.IsVisible = false;
						isUpdated = true;
					}
				}

				// 10件以上ある場合は古いものから削除
				if (EewCache.Count > 10)
				{
					var removes = new List<string>();
					foreach (var e in EewCache.OrderBy(e => e.Value.OccurrenceTime).Take(EewCache.Count - 10)
								 .Select(e => e.Key))
					{
						removes.Add(e);
						isUpdated = true;
					}
					foreach (var r in removes)
						EewCache.Remove(r);
				}

				if (isUpdated)
					EewUpdated?.Invoke((t, EewCache.Values.ToArray()));
			}
		};

		EewReceivedSound = soundPlayer.RegisterSound(SoundCategory, "EewReceived", "緊急地震速報受信", "{int}: 最大震度 [？,0,1,...,6-,6+,7]", new Dictionary<string, string> { { "int", "4" }, });
		EewBeginReceivedSound = soundPlayer.RegisterSound(SoundCategory, "EewBeginReceived", "緊急地震速報受信(初回)", "{int}: 最大震度 [-,0,1,...,6-,6+,7]", new Dictionary<string, string> { { "int", "5+" }, });
		EewFinalReceivedSound = soundPlayer.RegisterSound(SoundCategory, "EewFinalReceived", "緊急地震速報受信(最終)", "{int}: 最大震度 [-,0,1,...,6-,6+,7]", new Dictionary<string, string> { { "int", "-" }, });
		EewCanceledSound = soundPlayer.RegisterSound(SoundCategory, "EewCanceled", "緊急地震速報受信(キャンセル)");
	}

	/// <summary>
	/// 警報地域のみの更新を行う
	/// </summary>
	public void UpdateWarningAreas(IEew eew, DateTime updatedTime)
	{
		lock (EewCache)
		{
			if (!EewCache.TryGetValue(eew.Id, out var cEew))
				EewCache.Add(eew.Id, eew);
			else
			{
				cEew.ForecastIntensityMap = eew.ForecastIntensityMap;
				cEew.WarningAreaCodes = eew.WarningAreaCodes;
				cEew.WarningAreaNames = eew.WarningAreaNames;
			}
			EewUpdated?.Invoke((updatedTime, EewCache.Values.ToArray()));
		}
	}

	/// <summary>
	/// 複数のソースで発生したEEWを統合して管理する
	/// </summary>
	/// <param name="eew">発生したEEW / キャッシュのクリアチェックのみを行う場合はnull</param>
	/// <param name="updatedTime">そのEEWを受信した時刻</param>
	public void Update(IEew? eew, DateTime updatedTime)
	{
		lock (EewCache)
			if (UpdateInternal(eew, updatedTime))
				EewUpdated?.Invoke((updatedTime, EewCache.Values.ToArray()));
	}

	private bool UpdateInternal(IEew? eew, DateTime updatedTime)
	{
		var isUpdated = false;

		// 更新されたEEWが存在しなければそのまま終了
		if (string.IsNullOrWhiteSpace(eew?.Id))
		{
			// EEWが存在しない場合 NIED の過去のEEWはすべてキャンセル扱いとする
			foreach (var e in EewCache.Values.Where(e => e is KyoshinMonitorEew { IsFinal: false, IsCancelled: false } && e.UpdatedTime < updatedTime))
			{
				Logger.LogInfo($"NIEDからのリクエストでEEWをキャンセル扱いにしました: {e.Id}");
				if (e is KyoshinMonitorEew kme)
					kme.IsCancelled = true;
				e.UpdatedTime = updatedTime;
				isUpdated = true;

				if (!EewCanceledSound.Play())
					EewReceivedSound.Play(new Dictionary<string, string> { { "int", "？" } });
			}
			return isUpdated;
		}

		// 詳細を表示しない設定かつ1点での場合処理しない 警報･キャンセルのときのみ処理する
		if (!EewCache.ContainsKey(eew.Id) && !Config.Eew.ShowDetails && eew is { IsCancelled: false, IsWarning: false, LocationAccuracy: 1, DepthAccuracy: 1 })
		{
			Logger.LogInfo($"精度が低いEEWのため、スキップしました {eew.ToDetailString()}");
			return false;
		}

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
					if (!cEew2.IsFinal && !EewFinalReceivedSound.Play(new Dictionary<string, string> { { "int", intStr } }))
						EewReceivedSound.Play(new Dictionary<string, string> { { "int", intStr } });
				}
				else if (eew.IsCancelled)
				{
					if (!cEew2.IsCancelled && !EewCanceledSound.Play())
						EewReceivedSound.Play(new Dictionary<string, string> { { "int", "？" } });
				}
				else if (eew.Count > cEew2.Count)
					EewReceivedSound.Play(new Dictionary<string, string> { { "int", intStr } });
			}
			else if (!EewBeginReceivedSound.Play(new Dictionary<string, string> { { "int", intStr } }))
				EewReceivedSound.Play(new Dictionary<string, string> { { "int", intStr } });

			if (Config.Notification.EewReceived && Config.Timer.TimeshiftSeconds == 0)
			{
				if (eew.IsCancelled)
					NotificationService?.Notify($"緊急地震速報({eew.Count:00}報)", eew.IsTrueCancelled ? "キャンセルされました" : "キャンセルされたか、受信範囲外になりました");
				else
					NotificationService?.Notify($"緊急地震速報({eew.Count:00}報)", $"最大{eew.Intensity.ToLongString()}/{eew.Place}/M{eew.Magnitude:0.0}/{eew.Depth}km\n{eew.SourceDisplay}");
			}

			EventHook.Run("EEW_RECEIVED", new Dictionary<string, string> {
				{ "EEW_SOURCE", eew.SourceDisplay },
				{ "EEW_EVENT_ID", eew.Id },
				{ "EEW_COUNT", eew.Count.ToString() },
				{ "EEW_PLACE", eew.Place ?? "" },
				{ "EEW_INTENSITY", eew.Intensity.ToShortString() },
				{ "EEW_IS_FINAL", eew.IsFinal.ToString() },
				{ "EEW_IS_CANCEL", eew.IsCancelled.ToString() },
			}).ConfigureAwait(false);

			Logger.LogInfo($"EEWを更新しました {eew.ToDetailString()}");
			EewCache[eew.Id] = eew;
			isUpdated = true;
		}

		return isUpdated;
	}
}
