using DynamicData;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Workflow;
using KyoshinEewViewer.Services;
using KyoshinMonitorLib;
using Splat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ZLinq;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;

public class EewController
{
	public bool IsReplay { get; set; }
	protected ILogger Logger { get; }

	private KyoshinMonitorSeries Series { get; }
	private KyoshinEewViewerConfiguration Config { get; }
	private WorkflowService WorkflowService { get; }
	private NotificationService NotificationService { get; }
	private SoundCategory SoundCategory { get; } = new("Eew", "緊急地震速報");

	private Lock _lock = new();
	private Dictionary<string, Models.Eew> EewCache { get; } = [];
	private Dictionary<string, Models.Eew> WarningEewCache { get; } = [];
	/// <summary>
	/// 発生中のEEWが存在するか
	/// </summary>
	public bool Found => EewCache.Count > 0 || WarningEewCache.Count > 0;

	private Sound EewReceivedSound { get; }
	private Sound EewBeginReceivedSound { get; }
	private Sound EewFinalReceivedSound { get; }
	private Sound EewCanceledSound { get; }

	public event Action<DateTime, Models.Eew[]>? EewUpdated;

	public EewController(
		ILogManager logManager,
		KyoshinMonitorSeries series,
		KyoshinEewViewerConfiguration config,
		NotificationService notificationService,
		SoundPlayerService soundPlayer,
		WorkflowService workflowService)
	{
		Logger = logManager.GetLogger<EewController>();
		Series = series;
		Config = config;
		WorkflowService = workflowService;
		NotificationService = notificationService;

		EewReceivedSound = soundPlayer.RegisterSound(SoundCategory, "EewReceived", "緊急地震速報受信", "{int}: 最大震度 [？,0,1,...,6-,6+,7]", new() { { "int", "4" }, });
		EewBeginReceivedSound = soundPlayer.RegisterSound(SoundCategory, "EewBeginReceived", "緊急地震速報受信(初回)", "{int}: 最大震度 [-,0,1,...,6-,6+,7]", new() { { "int", "5+" }, });
		EewFinalReceivedSound = soundPlayer.RegisterSound(SoundCategory, "EewFinalReceived", "緊急地震速報受信(最終)", "{int}: 最大震度 [-,0,1,...,6-,6+,7]", new() { { "int", "-" }, });
		EewCanceledSound = soundPlayer.RegisterSound(SoundCategory, "EewCanceled", "緊急地震速報受信(キャンセル)");
	}

	// 古い EEW を消すためのタイマーが発火するイベント
	public void TimerElapsed(DateTime t)
	{
		lock (_lock)
		{
			var isUpdated = false;
			foreach (var e in EewCache.Values.ToArray())
			{
				var diff = t - e.ReceiveTime;
				// 3分経過していれば削除
				if (diff >= TimeSpan.FromMinutes(3))
				{
					Logger.LogInfo($"EEW終了(期限切れ): {e.Id} {e.Source} {diff.TotalSeconds:0.000}s");
					EewCache.Remove(e.Id);
					// 警報を受信していた場合合わせて削除
					if (WarningEewCache.TryGetValue(e.Id, out var wEew))
					{
						Logger.LogInfo($"EEW警報終了(連動): {wEew.Id} {wEew.Source} {diff.TotalSeconds:0.000}s");
						WarningEewCache.Remove(wEew.Id);
					}
					isUpdated = true;
				}
			}
			foreach (var e in WarningEewCache.Values.ToArray())
			{
				var diff = t - e.ReceiveTime;
				// 警報のみの場合は3分経過していれば削除
				if (diff >= TimeSpan.FromMinutes(3))
				{
					Logger.LogInfo($"EEW警報終了(期限切れ): {e.Id} {e.Source} {diff.TotalSeconds:0.000}s");
					WarningEewCache.Remove(e.Id);
					isUpdated = true;
				}
			}

			if (isUpdated)
				EewUpdated?.Invoke(t, EewCache.Values.ToArray());
		}
	}

	/// <summary>
	/// 複数のソースで発生したEEWを統合して管理する
	/// </summary>
	/// <param name="eew">発生したEEW / キャッシュのクリアチェックのみを行う場合はnull</param>
	/// <param name="updatedTime">そのEEWを受信した時刻</param>
	public void Update(Models.Eew eew, DateTime updatedTime)
	{
		lock (_lock)
		{
			// 詳細を表示しない設定かつ1点での場合処理しない
			if (!EewCache.ContainsKey(eew.Id) &&
				!Config.Eew.ShowDetails &&
				eew is { IsCancelled: false, IsWarning: false, Hypocenter.Accuracy.LocationAccuracy: 1, Hypocenter.Accuracy.DepthAccuracy: 1 })
			{
				Logger.LogInfo($"精度が低いEEWのため、スキップしました {eew.Id}");
				return;
			}

			var intStr = eew.MaxIntensity.ToShortString().Replace('*', '-');

			// 同じイベントのEEWが存在する場合は更新を試みる
			if (EewCache.TryGetValue(eew.Id, out var cEew))
			{
				// すでに過去データが存在していて、更新がなければそのまま戻る
				if (!(MixEew(cEew, eew) is { } m))
					return;
				eew = m.Item2;

				if (eew.IsFinal)
				{
					if (!cEew.IsFinal)
					{
						if (!EewFinalReceivedSound.Play(new() { { "int", intStr } }))
							EewReceivedSound.Play(new() { { "int", intStr } });
						WorkflowService.PublishEvent(EewEvent.FromEewModel(Series, EewEventType.Final, eew, IsReplay));
					}
				}
				else if (m.Item1 == EewUpdateReason.NewerSerial)
				{
					EewReceivedSound.Play(new() { { "int", intStr } });
					WorkflowService.PublishEvent(EewEvent.FromEewModel(Series, EewEventType.UpdateNewSerial, eew, IsReplay));
				}
				else if (m.Item1 == EewUpdateReason.MorePriority)
					WorkflowService.PublishEvent(EewEvent.FromEewModel(Series, EewEventType.UpdateWithMoreAccurate, eew, IsReplay));

				// 警報状態になっていた場合
				if (cEew.IsWarning != true && eew.IsWarning)
					WorkflowService.PublishEvent(EewEvent.FromEewModel(Series, EewEventType.WarningLevelReached, eew, IsReplay));

				// 予想最大震度変更
				if (cEew.MaxIntensity < eew.MaxIntensity)
					WorkflowService.PublishEvent(EewEvent.FromEewModel(Series, EewEventType.IncreaseMaxIntensity, eew, IsReplay));
				else if (cEew.MaxIntensity > eew.MaxIntensity)
					WorkflowService.PublishEvent(EewEvent.FromEewModel(Series, EewEventType.DecreaseMaxIntensity, eew, IsReplay));
			}
			else
			{
				// 新規に受信した場合
				if (!EewBeginReceivedSound.Play(new() { { "int", intStr } }))
					EewReceivedSound.Play(new() { { "int", intStr } });
				WorkflowService.PublishEvent(EewEvent.FromEewModel(Series, EewEventType.New, eew, IsReplay));

				// 警報状態で発表されたパターン
				if (eew.IsWarning)
					WorkflowService.PublishEvent(EewEvent.FromEewModel(Series, EewEventType.WarningLevelReached, eew, IsReplay));
			}

			if (Config.Notification.EewReceived)
			{
				if (!eew.IsCancelled)
					NotificationService?.Notify($"緊急地震速報({eew.SerialNo:00}報)", $"最大{eew.MaxIntensity.ToLongString()}/{eew.Hypocenter?.Place}/M{eew.Hypocenter?.Magnitude:0.0}/{eew.Hypocenter?.Depth}km\n{eew.DisplaySource}");
			}

			Logger.LogInfo($"EEWを更新しました {eew.Id} {eew.Source}");
			EewCache[eew.Id] = eew;

			InvokeEewUpdated(updatedTime);
		}
	}

	/// <summary>
	/// EEWをキャンセルする
	/// </summary>
	/// <param name="eventId">キャンセルされた EEW の ID</param>
	/// <param name="updatedTime">そのEEWを受信した時刻</param>
	public void Cancelled(string? eventId, DateTime updatedTime)
	{
		lock (_lock)
		{
			// 更新されたEEWが存在しなければそのまま終了
			if (string.IsNullOrWhiteSpace(eventId))
			{
				var isUpdated = false;
				// EEWが存在しない場合強震モニタの過去のEEWはすべてキャンセル扱いとする
				var targetEews = EewCache.Values.Where(e => e is { Source: EewSource.KyoshinMonitor, IsFinal: false, IsCancelled: false } && e.ReceiveTime < updatedTime).ToArray();
				foreach (var e in targetEews)
				{
					Logger.LogInfo($"NIEDからのリクエストでEEWをキャンセル扱いにしました: {e.Id}");
					var newEew = e with { IsCancelled = true, IsTrueCancelled = false, ReceiveTime = updatedTime };
					EewCache[e.Id] = newEew;
					isUpdated = true;

					if (!EewCanceledSound.Play())
						EewReceivedSound.Play(new() { { "int", "？" } });
					WorkflowService.PublishEvent(EewEvent.FromEewModel(Series, EewEventType.Cancel, newEew, IsReplay));
					if (Config.Notification.EewReceived)
						NotificationService?.Notify($"緊急地震速報({e.SerialNo:00}報)", e.IsTrueCancelled ? "キャンセルされました" : "キャンセルされたか、受信範囲外になりました");
				}
				if (isUpdated)
					InvokeEewUpdated(updatedTime);
				return;
			}

			if (!EewCache.TryGetValue(eventId, out var eew) || eew.IsCancelled)
				return;
			
			var newEew2 = eew with { IsCancelled = true, IsTrueCancelled = true, ReceiveTime = updatedTime };
			Logger.LogInfo($"EEWをキャンセルしました: {eventId}");
			EewCache[eventId] = newEew2;
			var intstr = newEew2.MaxIntensity.ToShortString().Replace('*', '-');
			if (!EewCanceledSound.Play())
				EewReceivedSound.Play(new() { { "int", intstr } });
			WorkflowService.PublishEvent(EewEvent.FromEewModel(Series, EewEventType.Cancel, newEew2, IsReplay));
			if (Config.Notification.EewReceived)
				NotificationService?.Notify($"緊急地震速報({newEew2.SerialNo:00}報)", newEew2.IsTrueCancelled ? "キャンセルされました" : "キャンセルされたか、受信範囲外になりました");
			InvokeEewUpdated(updatedTime);
		}
	}

	/// <summary>
	/// 警報地域のみの更新を行う
	/// </summary>
	public void UpdateWarning(Models.Eew eew, DateTime updatedTime)
	{
		lock (_lock)
		{
			var cEew = WarningEewCache.GetValueOrDefault(eew.Id);
			if (cEew != null && cEew.SerialNo >= eew.SerialNo)
				return;

			WarningEewCache[eew.Id] = eew;
			WorkflowService.PublishEvent(EewEvent.FromEewModel(
				Series,
				cEew == null ? EewEventType.NewWarning : EewEventType.UpdateWarning,
				eew,
				IsReplay
			));
			InvokeEewUpdated(updatedTime);
		}
	}

	public void WarningCancelled(string eventId, DateTime updatedTime)
	{
		lock (_lock)
		{
			if (WarningEewCache.TryGetValue(eventId, out var eew) && !eew.IsCancelled)
			{
				var cEew = eew with { IsCancelled = true, IsTrueCancelled = true, ReceiveTime = updatedTime };

				WorkflowService.PublishEvent(EewEvent.FromEewModel(Series, EewEventType.CancelWarning, eew, IsReplay));
				WarningEewCache[eventId] = cEew;
				InvokeEewUpdated(updatedTime);
			}
		}
	}

	private enum EewUpdateReason
	{
		None = 0,
		NewerSerial,
		AccuracySupport,
		MorePriority,
	}

	/// <summary>
	/// EEW を統合する
	/// </summary>
	/// <param name="current"></param>
	/// <param name="received"></param>
	/// <returns>更新した EEW 更新しなかった場合は null</returns>
	private (EewUpdateReason, Models.Eew)? MixEew(Models.Eew current, Models.Eew received)
	{
		// 新しいデータの場合は問答無用で更新
		var isNewerSerial = received.SerialNo > current.SerialNo;
		if (isNewerSerial)
			return (EewUpdateReason.NewerSerial, received with { IsCancelled = received.IsCancelled || current.IsCancelled, IsTrueCancelled = received.IsTrueCancelled || current.IsTrueCancelled });
		if (received.SerialNo < current.SerialNo)
			return null;

		return (current.Source, received.Source) switch
		{
			(EewSource.KyoshinMonitor, EewSource.Dmdata)
				=> (EewUpdateReason.MorePriority, received with { IsCancelled = current.IsCancelled, IsTrueCancelled = current.IsTrueCancelled }),

			// 同じ報数で SNP を受信した場合は精度情報を移植する
			(EewSource.KyoshinMonitor, EewSource.SignalNowProfessional) or (EewSource.Axis, EewSource.SignalNowProfessional)
				=> (EewUpdateReason.AccuracySupport, current with
				{
					IsCancelled = current.IsCancelled || received.IsCancelled,
					IsTrueCancelled = current.IsTrueCancelled || received.IsTrueCancelled,
					Hypocenter = current.Hypocenter! with { Accuracy = received.Hypocenter?.Accuracy },
					WarningAreas = received.WarningAreas,
					ReceiveTime = received.ReceiveTime,
				}),

			// SNP で受信中から強震モニタ･AXISで受信した場合は強震モニタの情報を優先してそこに精度情報を埋め込む
			(EewSource.SignalNowProfessional, EewSource.KyoshinMonitor) or (EewSource.SignalNowProfessional, EewSource.Axis) or (EewSource.KyoshinMonitor, EewSource.Axis)
				=> (EewUpdateReason.AccuracySupport, received with
				{
					IsCancelled = current.IsCancelled || received.IsCancelled,
					IsTrueCancelled = current.IsTrueCancelled || received.IsTrueCancelled,
					Hypocenter = received.Hypocenter! with { Accuracy = current.Hypocenter?.Accuracy },
					WarningAreas = current.WarningAreas,
				}),

			// SNP･AXIS より dmdata を優先させる
			(EewSource.SignalNowProfessional, EewSource.Dmdata) or (EewSource.Axis, EewSource.Dmdata)
				=> (EewUpdateReason.MorePriority, received with
				{
					IsCancelled = current.IsCancelled || received.IsCancelled,
					IsTrueCancelled = received.IsCancelled || current.IsTrueCancelled,
				}),

			// そのほかの場合は何もしない
			_ => null,
		};
	}

	private void InvokeEewUpdated(DateTime updatedTime)
	{
		var eews = EewCache.Values.ToList();
		foreach (var e in eews.ToArray())
		{
			if (!WarningEewCache.TryGetValue(e.Id, out var wEew))
				continue;
			var mEew = e with { WarningAreas = wEew.WarningAreas };
			eews.Replace(e, mEew);
		}
		foreach (var e in WarningEewCache.Values.ToArray())
		{
			if (eews.Any(n => n.Id == e.Id))
				continue;
			eews.Add(e);
		}

		EewUpdated?.Invoke(updatedTime, eews.ToArray());
	}

	public void Clear()
	{
		lock (_lock)
		{
			EewCache.Clear();
			WarningEewCache.Clear();
		}
	}
}
