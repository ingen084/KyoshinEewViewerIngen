using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Workflow;
using KyoshinEewViewer.Services;
using KyoshinMonitorLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;

public class EewController : IEewController
{
	public bool IsReplay { get; private set; }

	public EewPointForecastController? PointForecastController { get; set; }

	protected ILogger Logger { get; }

	private KyoshinMonitorSeries Series { get; }
	private KyoshinEewViewerConfiguration Config { get; }
	private WorkflowService WorkflowService { get; }
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
	private Sound EewWarningAnnouncedSound { get; }
	private string SoundMode { get; }

	public event Action<DateTime, Models.Eew[]>? EewUpdated;

	public EewController(
		ILogger<EewController> logger,
		KyoshinMonitorSeries series,
		KyoshinEewViewerConfiguration config,
		SoundPlayerService soundPlayer,
		WorkflowService workflowService,
		bool isReplay = false)
	{
		Logger = logger;
		Series = series;
		Config = config;
		WorkflowService = workflowService;
		IsReplay = isReplay;
		SoundMode = isReplay ? "replay" : "realtime";

		EewReceivedSound = soundPlayer.RegisterSound(SoundCategory, "EewReceived", "緊急地震速報受信", "{int}: 最大震度 [？,0,1,...,6-,6+,7]\n{training}: 訓練かどうか [training, normal]", new() { { "int", "4" }, { "training", "normal" }, });
		EewBeginReceivedSound = soundPlayer.RegisterSound(SoundCategory, "EewBeginReceived", "緊急地震速報受信(初回)", "{int}: 最大震度 [-,0,1,...,6-,6+,7]\n{training}: 訓練かどうか [training, normal]", new() { { "int", "5+" }, { "training", "normal" }, });
		EewFinalReceivedSound = soundPlayer.RegisterSound(SoundCategory, "EewFinalReceived", "緊急地震速報受信(最終)", "{int}: 最大震度 [-,0,1,...,6-,6+,7]\n{training}: 訓練かどうか [training, normal]", new() { { "int", "-" }, { "training", "normal" }, });
		EewCanceledSound = soundPlayer.RegisterSound(SoundCategory, "EewCanceled", "緊急地震速報受信(キャンセル)", "{training}: 訓練かどうか [training, normal]", new() { { "training", "normal" }, });
		EewWarningAnnouncedSound = soundPlayer.RegisterSound(SoundCategory, "EewWarningAnnounced", "緊急地震速報(警報)発表･レベル到達時", "{int}: 最大震度 [？,0,1,...,6-,6+,7]\n{mode}: 再生モード [replay, realtime]\n{training}: 訓練かどうか [training, normal]", new() { { "int", "5-" }, { "mode", "replay" }, { "training", "normal" }, });

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
					Logger.LogInformation("EEW終了(期限切れ): {Id} {Source} {TotalSeconds:0.000}s", e.Id, e.Source, diff.TotalSeconds);
					EewCache.Remove(e.Key);
					// 警報を受信していた場合合わせて削除
					if (WarningEewCache.TryGetValue(e.Key, out var wEew))
					{
						Logger.LogInformation("EEW警報終了(連動): {Id} {Source} {TotalSeconds:0.000}s", wEew.Id, wEew.Source, diff.TotalSeconds);
						WarningEewCache.Remove(wEew.Key);
					}
					isUpdated = true;
				}
			}
			foreach (var e in WarningEewCache.Values.ToArray())
			{
				// 予報電文が残っている場合は予報電文の期限切れに合わせて削除するため、ここでは対象にしない
				if (EewCache.ContainsKey(e.Key))
					continue;

				var diff = t - e.ReceiveTime;
				// 警報のみの場合は3分経過していれば削除
				if (diff >= TimeSpan.FromMinutes(3))
				{
					Logger.LogInformation("EEW警報終了(期限切れ): {Id} {Source} {TotalSeconds:0.000}s", e.Id, e.Source, diff.TotalSeconds);
					WarningEewCache.Remove(e.Key);
					isUpdated = true;
				}
			}

			if (isUpdated)
				InvokeEewUpdated(t);
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
			if (!AcceptWithTrainingPriority(eew, out var isTrainingEnded))
				return;

			// 詳細を表示しない設定かつ1点での場合処理しない
			if (!EewCache.ContainsKey(eew.Key) &&
				!Config.Eew.ShowDetails &&
				eew is { IsCancelled: false, IsWarning: false, Hypocenter.Accuracy.LocationAccuracy: 1, Hypocenter.Accuracy.DepthAccuracy: 1 })
			{
				Logger.LogInformation("精度が低いEEWのため、スキップしました {Id}", eew.Id);
				if (isTrainingEnded)
					InvokeEewUpdated(updatedTime);
				return;
			}

			var intStr = eew.MaxIntensity.ToShortString().Replace('*', '-');
			var training = ToTrainingSoundParameter(eew);

			// 同じイベントのEEWが存在する場合は更新を試みる
			if (EewCache.TryGetValue(eew.Key, out var cEew))
			{
				// すでに過去データが存在していて、更新がなければそのまま戻る
				if (!(MixEew(cEew, eew) is { } m))
				{
					if (isTrainingEnded)
						InvokeEewUpdated(updatedTime);
					return;
				}
				eew = m.Item2;

				if (eew.IsFinal)
				{
					if (!cEew.IsFinal)
					{
						if (!EewFinalReceivedSound.Play(new() { { "int", intStr }, { "training", training } }))
							EewReceivedSound.Play(new() { { "int", intStr }, { "training", training } });
						WorkflowService.PublishEvent(EewEvent.FromEewModel(Series, EewEventType.Final, eew, IsReplay, this));
					}
				}
				else if (m.Item1 == EewUpdateReason.NewerSerial)
				{
					EewReceivedSound.Play(new() { { "int", intStr }, { "training", training } });
					WorkflowService.PublishEvent(EewEvent.FromEewModel(Series, EewEventType.UpdateNewSerial, eew, IsReplay, this));
				}
				else if (m.Item1 == EewUpdateReason.MorePriority)
					WorkflowService.PublishEvent(EewEvent.FromEewModel(Series, EewEventType.UpdateWithMoreAccurate, eew, IsReplay, this));

				// 警報状態になっていた場合
				if (cEew.IsWarning != true && eew.IsWarning)
				{
				if (!WarningEewCache.ContainsKey(eew.Key))
					EewWarningAnnouncedSound.Play(new() { { "int", intStr }, { "mode", SoundMode }, { "training", training } });
					WorkflowService.PublishEvent(EewEvent.FromEewModel(Series, EewEventType.WarningLevelReached, eew, IsReplay, this));
				}

				// 予想最大震度変更
				if (cEew.MaxIntensity < eew.MaxIntensity)
					WorkflowService.PublishEvent(EewEvent.FromEewModel(Series, EewEventType.IncreaseMaxIntensity, eew, IsReplay, this));
				else if (cEew.MaxIntensity > eew.MaxIntensity)
					WorkflowService.PublishEvent(EewEvent.FromEewModel(Series, EewEventType.DecreaseMaxIntensity, eew, IsReplay, this));
			}
			else
			{
				// 新規に受信した場合
				if (!EewBeginReceivedSound.Play(new() { { "int", intStr }, { "training", training } }))
					EewReceivedSound.Play(new() { { "int", intStr }, { "training", training } });
				WorkflowService.PublishEvent(EewEvent.FromEewModel(Series, EewEventType.New, eew, IsReplay, this));

				// 警報状態で発表されたパターン
				if (eew.IsWarning)
				{
				if (!WarningEewCache.ContainsKey(eew.Key))
					EewWarningAnnouncedSound.Play(new() { { "int", intStr }, { "mode", SoundMode }, { "training", training } });
					WorkflowService.PublishEvent(EewEvent.FromEewModel(Series, EewEventType.WarningLevelReached, eew, IsReplay, this));
				}
			}

			Logger.LogInformation("EEWを更新しました {Id} {Source}", eew.Id, eew.Source);
			EewCache[eew.Key] = eew;

			InvokeEewUpdated(updatedTime);
		}
	}

	/// <summary>
	/// EEWをキャンセルする
	/// </summary>
	/// <param name="key">キャンセルされた EEW のキー (<see cref="Models.Eew.Key"/>) 訓練以外は地震IDと同じ / null または空の場合は強震モニタの過去のEEWをキャンセル扱いにする</param>
	/// <param name="updatedTime">そのEEWを受信した時刻</param>
	public void Cancelled(string? key, DateTime updatedTime)
	{
		lock (_lock)
		{
			// 更新されたEEWが存在しなければそのまま終了
			if (string.IsNullOrWhiteSpace(key))
			{
				var isUpdated = false;
				// EEWが存在しない場合強震モニタの過去のEEWはすべてキャンセル扱いとする
				var targetEews = EewCache.Values.Where(e => e is { Source: EewSource.KyoshinMonitor, IsFinal: false, IsCancelled: false } && e.ReceiveTime < updatedTime).ToArray();
				foreach (var e in targetEews)
				{
					Logger.LogInformation("NIEDからのリクエストでEEWをキャンセル扱いにしました: {Id}", e.Id);
					var newEew = e with { IsCancelled = true, IsTrueCancelled = false, ReceiveTime = updatedTime };
					EewCache[e.Key] = newEew;
					isUpdated = true;

					if (!EewCanceledSound.Play(new() { { "training", ToTrainingSoundParameter(newEew) } }))
						EewReceivedSound.Play(new() { { "int", "？" }, { "training", ToTrainingSoundParameter(newEew) } });
					WorkflowService.PublishEvent(EewEvent.FromEewModel(Series, EewEventType.Cancel, newEew, IsReplay, this));
				}
				if (isUpdated)
					InvokeEewUpdated(updatedTime);
				return;
			}

			ApplyTrueCancel(key, updatedTime, false);
		}
	}

	/// <summary>
	/// 外部への送信が禁止されている受信元から受信したキャンセルを適用する
	/// キャンセルされた事実も受信した情報のため、他の受信元から受信した EEW に適用した場合も外部へ送信しない
	/// </summary>
	/// <param name="key">キャンセルされた EEW のキー (<see cref="Models.Eew.Key"/>)</param>
	/// <param name="updatedTime">そのEEWを受信した時刻</param>
	public void CancelledWithRelayProhibited(string key, DateTime updatedTime)
	{
		lock (_lock)
			ApplyTrueCancel(key, updatedTime, true);
	}

	private void ApplyTrueCancel(string key, DateTime updatedTime, bool isRelayProhibited)
	{
		if (!EewCache.TryGetValue(key, out var eew) || eew.IsTrueCancelled)
			return;

		var newEew = eew with { IsCancelled = true, IsTrueCancelled = true, IsCancellationRelayProhibited = isRelayProhibited, ReceiveTime = updatedTime };
		// 擬似キャンセル済みの場合は通知を済ませているため、確定キャンセルへの格上げのみ行う
		if (eew.IsCancelled)
		{
			Logger.LogInformation("擬似キャンセル中のEEWを確定キャンセルにしました: {EventId}", eew.Id);
			EewCache[key] = newEew;
			InvokeEewUpdated(updatedTime);
			return;
		}

		Logger.LogInformation("EEWをキャンセルしました: {EventId}", eew.Id);
		EewCache[key] = newEew;
		var intstr = newEew.MaxIntensity.ToShortString().Replace('*', '-');
		var training = ToTrainingSoundParameter(newEew);
		if (!EewCanceledSound.Play(new() { { "training", training } }))
			EewReceivedSound.Play(new() { { "int", intstr }, { "training", training } });
		WorkflowService.PublishEvent(EewEvent.FromEewModel(Series, EewEventType.Cancel, newEew, IsReplay, this));
		InvokeEewUpdated(updatedTime);
	}

	/// <summary>
	/// 警報地域のみの更新を行う
	/// </summary>
	public void UpdateWarning(Models.Eew eew, DateTime updatedTime)
	{
		lock (_lock)
		{
			if (!AcceptWithTrainingPriority(eew, out var isTrainingEnded))
				return;

			var cEew = WarningEewCache.GetValueOrDefault(eew.Key);
			if (cEew != null && cEew.SerialNo >= eew.SerialNo)
			{
				if (isTrainingEnded)
					InvokeEewUpdated(updatedTime);
				return;
			}

			WarningEewCache[eew.Key] = eew;
			if (cEew == null && (!EewCache.TryGetValue(eew.Key, out var forecastEew) || !forecastEew.IsWarning))
			{
				var intStr = eew.MaxIntensity.ToShortString().Replace('*', '-');
				EewWarningAnnouncedSound.Play(new() { { "int", intStr }, { "mode", SoundMode }, { "training", ToTrainingSoundParameter(eew) } });
			}
			WorkflowService.PublishEvent(EewEvent.FromEewModel(
				Series,
				cEew == null ? EewEventType.NewWarning : EewEventType.UpdateWarning,
				eew,
				IsReplay
			));
			InvokeEewUpdated(updatedTime);
		}
	}

	/// <summary>
	/// 警報をキャンセルする
	/// </summary>
	/// <param name="key">キャンセルされた EEW のキー (<see cref="Models.Eew.Key"/>)</param>
	/// <param name="updatedTime">そのEEWを受信した時刻</param>
	public void WarningCancelled(string key, DateTime updatedTime)
	{
		lock (_lock)
		{
			if (WarningEewCache.TryGetValue(key, out var eew) && !eew.IsCancelled)
			{
				var cEew = eew with { IsCancelled = true, IsTrueCancelled = true, ReceiveTime = updatedTime };

				WorkflowService.PublishEvent(EewEvent.FromEewModel(Series, EewEventType.CancelWarning, eew, IsReplay, this));
				WarningEewCache[key] = cEew;
				InvokeEewUpdated(updatedTime);
			}
		}
	}

	/// <summary>
	/// 表示中の訓練の EEW をすべて終了する
	/// </summary>
	/// <returns>終了した訓練の EEW があった場合は true</returns>
	public bool EndTraining(DateTime updatedTime)
	{
		lock (_lock)
		{
			if (!RemoveTrainingEews())
				return false;
			Logger.LogInformation("訓練のEEWを終了しました");
			InvokeEewUpdated(updatedTime);
			return true;
		}
	}

	/// <summary>
	/// 訓練以外の EEW を優先する
	/// 訓練以外の EEW を受信したら訓練の EEW を終了し、訓練以外の EEW の表示中は訓練の EEW を受け付けない
	/// ロックを取得した状態で呼び出すこと
	/// </summary>
	/// <param name="eew">受信した EEW</param>
	/// <param name="isTrainingEnded">訓練の EEW を終了した場合は true 呼び出し元で更新を通知すること</param>
	/// <returns>受信した EEW を処理する場合は true</returns>
	private bool AcceptWithTrainingPriority(Models.Eew eew, out bool isTrainingEnded)
	{
		isTrainingEnded = false;
		if (eew.IsTraining)
		{
			if (!EewCache.Values.Any(e => !e.IsTraining) && !WarningEewCache.Values.Any(e => !e.IsTraining))
				return true;
			Logger.LogInformation("訓練以外のEEWを表示中のため、訓練のEEWを無視しました: {Id}", eew.Id);
			return false;
		}

		isTrainingEnded = RemoveTrainingEews();
		if (isTrainingEnded)
			Logger.LogInformation("訓練以外のEEWを受信したため、訓練のEEWを終了しました: {Id}", eew.Id);
		return true;
	}

	/// <summary>
	/// 訓練の EEW をキャッシュから削除する ロックを取得した状態で呼び出すこと
	/// </summary>
	/// <returns>削除した場合は true</returns>
	private bool RemoveTrainingEews()
	{
		var keys = EewCache.Values.Concat(WarningEewCache.Values)
			.Where(e => e.IsTraining)
			.Select(e => e.Key)
			.Distinct()
			.ToArray();
		foreach (var key in keys)
		{
			EewCache.Remove(key);
			WarningEewCache.Remove(key);
		}
		return keys.Length > 0;
	}

	/// <summary>
	/// 音声ファイルのパスで訓練かどうかを区別するためのパラメータ値
	/// </summary>
	private static string ToTrainingSoundParameter(Models.Eew eew)
		=> eew.IsTraining ? "training" : "normal";

	private enum EewUpdateReason
	{
		None = 0,
		NewerSerial,
		AccuracySupport,
		MorePriority,
		ForecastSupport,
		CancelCleared,
	}

	/// <summary>
	/// EEW を統合する
	/// </summary>
	/// <param name="current"></param>
	/// <param name="received"></param>
	/// <returns>更新した EEW 更新しなかった場合は null</returns>
	private (EewUpdateReason, Models.Eew)? MixEew(Models.Eew current, Models.Eew received)
	{
		// AXIS は警報の解除を取消報でしか通知しないため、予報電文で警報状態を解除しない
		var mergedReceived = MergeAxisWarningState(current, received);

		// 新しいデータの場合は問答無用で更新
		var isNewerSerial = received.SerialNo > current.SerialNo;
		if (isNewerSerial)
			return (EewUpdateReason.NewerSerial, MergeCancellation(current, received, mergedReceived));
		if (received.SerialNo < current.SerialNo)
		{
			// AXIS の警報電文の報数は予報電文とは別に 1 から振られるため、報数が戻っていても
			// まだ警報状態が反映されていなければ警報電文の内容を取り込む
			if (current.Source == EewSource.Axis && received.Source == EewSource.Axis &&
				received.IsWarning && !current.IsWarning)
				return (EewUpdateReason.MorePriority, MergeCancellation(current, received, mergedReceived with { SerialNo = current.SerialNo }));
			return null;
		}

		return (current.Source, received.Source) switch
		{
			(EewSource.KyoshinMonitor, EewSource.Dmdata)
				=> (EewUpdateReason.MorePriority, MergeCancellation(current, received, received)),

			// 同じ報数で SNP を受信した場合は精度情報を移植する
			(EewSource.KyoshinMonitor, EewSource.SignalNowProfessional) or (EewSource.Axis, EewSource.SignalNowProfessional)
				=> (EewUpdateReason.AccuracySupport, MergeCancellation(
					current,
					received,
					current with
					{
						Hypocenter = current.Hypocenter! with { Accuracy = received.Hypocenter?.Accuracy },
						WarningAreas = received.WarningAreas,
						ReceiveTime = received.ReceiveTime,
					})),

			// SNP で受信中から強震モニタ･AXISで受信した場合は強震モニタの情報を優先してそこに精度情報を埋め込む
			(EewSource.SignalNowProfessional, EewSource.KyoshinMonitor) or (EewSource.SignalNowProfessional, EewSource.Axis) or (EewSource.KyoshinMonitor, EewSource.Axis)
				=> (EewUpdateReason.AccuracySupport, MergeCancellation(
					current,
					received,
					received with
					{
						Hypocenter = received.Hypocenter! with { Accuracy = current.Hypocenter?.Accuracy },
						WarningAreas = current.WarningAreas,
					})),

			// SNP･AXIS より dmdata を優先させる
			(EewSource.SignalNowProfessional, EewSource.Dmdata) or (EewSource.Axis, EewSource.Dmdata)
				=> (EewUpdateReason.MorePriority, MergeCancellation(current, received, received)),

			// AXIS の警報電文は予報電文とは別の報数体系のため、同じ報数でも警報状態のみ反映する
			(EewSource.Axis, EewSource.Axis) when received.IsWarning && !current.IsWarning
				=> (EewUpdateReason.MorePriority, MergeCancellation(current, received, mergedReceived with { SerialNo = current.SerialNo })),

			// EEW Client は dmdata の情報を中継したものなので dmdata と同等に扱い、強震モニタ･SNP･AXIS より優先させる
			// EEW Client には警報地域が含まれないため、SNP から受け取っていた分を引き継ぐ
			(EewSource.KyoshinMonitor, EewSource.DmdataEewClient) or (EewSource.SignalNowProfessional, EewSource.DmdataEewClient) or (EewSource.Axis, EewSource.DmdataEewClient)
				=> (EewUpdateReason.MorePriority, MergeCancellation(current, received, received with { WarningAreas = current.WarningAreas })),
			(EewSource.DmdataEewClient, EewSource.SignalNowProfessional) when current.WarningAreas == null && received.WarningAreas != null
				=> (EewUpdateReason.ForecastSupport, MergeCancellation(current, received, current with { WarningAreas = received.WarningAreas })),

			// EEW Client は PLUM 法の予想が発生すると同じ報数を再配信し、予想震度が変わることがある
			(EewSource.DmdataEewClient, EewSource.DmdataEewClient)
				=> (EewUpdateReason.ForecastSupport, MergeCancellation(
					current,
					received,
					received with
					{
						IntensityForecastMap = current.IntensityForecastMap,
						WarningAreas = current.WarningAreas,
					})),

			// dmdata の予報電文と EEW Client は同じ報数であれば同じ電文に由来し震源が一致するため、予想震度を補い合う
			// EEW Client には警報地域も含まれないため、予報電文から補う
			(EewSource.Dmdata, EewSource.DmdataEewClient) when received.CalculatedIntensityForecastMap != null
				=> (EewUpdateReason.ForecastSupport, MergeCancellation(
					current,
					received,
					current with
					{
						CalculatedIntensityForecastMap = received.CalculatedIntensityForecastMap,
						CalculatedIntensityForecastSource = received.CalculatedIntensityForecastSource,
					})),
			(EewSource.DmdataEewClient, EewSource.Dmdata) when (current.IntensityForecastMap == null && received.IntensityForecastMap != null) || (current.WarningAreas == null && received.WarningAreas != null)
				=> (EewUpdateReason.ForecastSupport, MergeCancellation(
					current,
					received,
					current with
					{
						IntensityForecastMap = current.IntensityForecastMap ?? received.IntensityForecastMap,
						WarningAreas = current.WarningAreas ?? received.WarningAreas,
					})),

			_ when ShouldClearCancel(current, received)
				=> (EewUpdateReason.CancelCleared, MergeCancellation(current, received, mergedReceived)),

			// そのほかの場合は何もしない
			_ => null,
		};
	}

	private static bool ShouldClearCancel(Models.Eew current, Models.Eew received)
		=> current is { IsCancelled: true, IsTrueCancelled: false } &&
		   received is { IsCancelled: false, IsTrueCancelled: false };

	/// <summary>
	/// AXIS は警報の解除を取消報でしか通知しないため、警報受信後の予報電文で警報状態を解除しない
	/// </summary>
	private static Models.Eew MergeAxisWarningState(Models.Eew current, Models.Eew received)
		=> current is { Source: EewSource.Axis, IsWarning: true } && received.Source == EewSource.Axis
			? received with { IsWarning = true }
			: received;

	private static Models.Eew MergeCancellation(Models.Eew current, Models.Eew received, Models.Eew merged)
	{
		var isTrueCancelled = current.IsTrueCancelled || received.IsTrueCancelled;
		return merged with
		{
			IsCancelled = isTrueCancelled || received.IsCancelled,
			IsTrueCancelled = isTrueCancelled,
			IsCancellationRelayProhibited = current.IsCancellationRelayProhibited,
		};
	}

	/// <summary>
	/// 表示直前に EEW へ合成する情報を提供する
	/// 地点予測の合成に使用する
	/// </summary>
	public Func<Models.Eew, Models.Eew>? MergeAdditionalInformation { get; set; }

	/// <summary>
	/// 指定したキーのEEWを取得する
	/// </summary>
	/// <param name="key">EEW のキー (<see cref="Models.Eew.Key"/>) 訓練以外は地震IDと同じ</param>
	public bool TryGetEew(string key, out Models.Eew? eew)
	{
		lock (_lock)
			return EewCache.TryGetValue(key, out eew);
	}

	/// <summary>
	/// 保持している内容で更新通知を発火する
	/// 地点予測のみが更新された場合に使用する
	/// </summary>
	public void RefreshEewUpdated(DateTime updatedTime)
	{
		lock (_lock)
			InvokeEewUpdated(updatedTime);
	}

	private void InvokeEewUpdated(DateTime updatedTime)
	{
		var eews = EewCache.Values.ToList();
		foreach (var e in eews.ToArray())
		{
			// 警報情報が存在する場合はくっつける
			if (!WarningEewCache.TryGetValue(e.Key, out var wEew) || wEew.WarningAreas?.IsWarningTelegram != true)
				continue;
			var mEew = e with { WarningAreas = wEew.WarningAreas };
			eews[eews.IndexOf(e)] = mEew;
		}
		foreach (var e in WarningEewCache.Values.ToArray())
		{
			if (eews.Any(n => n.Key == e.Key))
				continue;
			eews.Add(e);
		}

		var preferCalculated = Config.Eew.PreferCalculatedIntensityForecast;
		for (var i = 0; i < eews.Count; i++)
			if (eews[i].PreferCalculatedIntensityForecast != preferCalculated)
				eews[i] = eews[i] with { PreferCalculatedIntensityForecast = preferCalculated };

		if (MergeAdditionalInformation is { } merge)
			for (var i = 0; i < eews.Count; i++)
				eews[i] = merge(eews[i]);

		EewUpdated?.Invoke(updatedTime, eews.ToArray());
	}

	public void Clear()
	{
		lock (_lock)
		{
			EewCache.Clear();
			WarningEewCache.Clear();
		}
		Cleared?.Invoke();
	}

	/// <summary>
	/// キャッシュがクリアされた
	/// </summary>
	public event Action? Cleared;
}
