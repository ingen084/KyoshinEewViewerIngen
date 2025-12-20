using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinMonitorLib;
using KyoshinMonitorLib.SkiaImages;
using SkiaSharp;
using Splat;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Models;

/// <summary>
/// 揺れ検知エンジン
/// パラメータに基づいて観測点データから揺れイベントを検出する
/// </summary>
public class ShakeDetectionEngine(ILogManager? logManager = null, ShakeDetectionParameters? parameters = null)
{
	private ILogger? Logger { get; } = logManager?.GetLogger<ShakeDetectionEngine>() ?? null;

	/// <summary>
	/// 使用するパラメータ
	/// </summary>
	public ShakeDetectionParameters Parameters { get; set; } = parameters ?? ShakeDetectionParameters.Default;

	/// <summary>
	/// 検出されたイベント
	/// </summary>
	public List<KyoshinEvent> KyoshinEvents { get; } = [];

	/// <summary>
	/// 観測点
	/// </summary>
	public RealtimeObservationPoint[]? Points { get; private set; }

	/// <summary>
	/// 観測点を初期化する
	/// </summary>
	public void Initialize(RealtimeObservationPoint[] sourcePoints)
	{
		Points = sourcePoints;
		SetupNearPoints();
		KyoshinEvents.Clear();
	}

	/// <summary>
	/// 近傍観測点を設定する
	/// </summary>
	private void SetupNearPoints()
	{
		if (Points == null)
			return;

		foreach (var point in Points)
		{
			point.NearPoints = Points
				.Where(p => point != p && point.Location.Distance(p.Location) < Parameters.MaxSearchDistance)
				.OrderBy(p => point.Location.Distance(p.Location))
				.Take(Parameters.MaxNearPoints)
				.Select(p =>
				{
					var distance = point.Location.Distance(p.Location);
					return new RealtimeObservationPoint.NearPointInfo(p, distance, Parameters.CalculateDistanceWeight(distance));
				})
				.ToArray();
		}
	}

	/// <summary>
	/// 画像を処理して揺れを検出する
	/// </summary>
	public void ProcessImage(SKBitmap bitmap, DateTime time)
	{
		if (Points == null)
			return;

		// パース
		foreach (var point in Points)
		{
			var color = bitmap.GetPixel(point.ImageLocation.X, point.ImageLocation.Y);
			if (color.Alpha != 255)
			{
				point.Update(null, null);
				continue;
			}
			var intensity = ColorConverter.ConvertToIntensityFromScale(ColorConverter.ConvertToScaleAtPolynomialInterpolation(color));
			point.Update(color, (float)intensity);
		}

		ProcessEvents(time);
	}

	/// <summary>
	/// イベント検出処理
	/// </summary>
	private void ProcessEvents(DateTime time)
	{
		if (Points == null)
			return;

		// イベントチェック･異常値除外
		foreach (var point in Points)
		{
			// 異常値の排除
			if (point.LatestIntensity is { } latestIntensity &&
				point.IntensityDiff < 1 && point.Event == null &&
				latestIntensity >= (point.HasNearPoints ? 3 : 5) && // 震度3以上 離島は5以上
				Math.Abs(point.IntensityAverage - latestIntensity) <= 1 && // 10秒間平均で 1.0 の範囲
				(
					point.IsTmpDisabled || (point.NearPoints?.All(p => (latestIntensity - (p.Point.LatestIntensity ?? -3)) >= 3) ?? true)
				))
			{
				if (!point.IsTmpDisabled)
					Logger?.LogInfo($"異常値の判定により観測点の除外を行いました: {point.Code} {point.LatestIntensity} {point.IntensityAverage}");
				point.IsTmpDisabled = true;
			}
			else if (point.LatestIntensity != null && point.IsTmpDisabled)
			{
				Logger?.LogInfo($"異常値による除外を戻します: {point.Code} {point.LatestIntensity} {point.IntensityAverage}");
				point.IsTmpDisabled = false;
			}

			// 除外されている観測点はイベントの検出に使用しない
			if (point.IsTmpDisabled)
				continue;

			if (point.IntensityDiff < Parameters.MinDetectionDiff)
			{
				// 未来もしくは過去のイベントは離脱
				if (point.Event is { } evt && (point.EventedAt > time || point.EventedExpireAt < time))
				{
					Logger?.LogDebug($"揺れ検知終了: {point.Code} {evt.Id} {time} {point.EventedAt} {point.EventedExpireAt}");
					point.Event = null;
					evt.RemovePoint(point);

					if (evt.PointCount <= 0)
					{
						KyoshinEvents.Remove(evt);
						Logger?.LogDebug($"イベント終了: {evt.Id}");
					}
				}
				continue;
			}
			// 周囲の観測点が未計算の場合もしくは欠測の場合戻る
			if (point.NearPoints == null || point.LatestIntensity == null)
				continue;

			// 有効な近傍観測点の重み合計を計算
			var availableTotalWeight = point.NearPoints
				.Where(n => n.Point.HasValidHistory)
				.Sum(n => n.Weight);

#if DEBUG
			point.DebugAvailableTotalWeight = availableTotalWeight;
#endif

			// 重み合計が極端に低い場合は離島扱い（単独検知）
			if (availableTotalWeight < Parameters.IsolatedThreshold)
			{
#if DEBUG
				point.DebugIsIsolated = true;
				point.DebugDetectionScore = 0;
				point.DebugDetectionThreshold = Parameters.IsolatedDetectionDiff;
#endif
				if (point.IntensityDiff >= Parameters.IsolatedDetectionDiff && point.Event == null)
				{
					var level = KyoshinEvent.GetLevel(point.LatestIntensity);
					point.Event = new(time, point, Parameters.GetSeconds(level)) { IsConfirmed = level > KyoshinEventLevel.Weak };
					point.EventedAt = time;
					KyoshinEvents.Add(point.Event);
					Logger?.LogDebug($"揺れ検知(単独): {point.Code} 変位: {point.IntensityDiff} {point.Event.Id}");
				}
				continue;
			}
#if DEBUG
			point.DebugIsIsolated = false;
#endif

			var events = new List<KyoshinEvent>();
			if (point.Event != null)
				events.Add(point.Event);

			// 距離・震度上昇度重み付きスコアを計算
			// 近傍観測点が無反応の場合はペナルティを適用
			var score = 0d;
			var penaltyScore = 0d;
			var contributingPointCount = 0;
			foreach (var np in point.NearPoints)
			{
				if (np.Point.IsTmpDisabled || !np.Point.HasValidHistory)
					continue;

				if (np.Point.IntensityDiff >= Parameters.MinDetectionDiff)
				{
					score += np.Weight * (np.Point.IntensityDiff - Parameters.ScoreIntensityOffset);
					contributingPointCount++;
					if (np.Point.Event != null)
						events.Add(np.Point.Event);
				}
				else
				{
					// 近い観測点が無反応の場合、距離重みに応じたペナルティ
					penaltyScore += np.Weight * Parameters.NoChangePenaltyFactor;
				}
			}

			// ペナルティを適用した最終スコア
			var finalScore = score - penaltyScore;

			// 閾値: 有効な重み合計に係数を掛けた値
			var threshold = availableTotalWeight * Parameters.ScoreThresholdRatio;

#if DEBUG
			point.DebugDetectionScore = finalScore;
			point.DebugDetectionThreshold = threshold;
			point.DebugNoChangePenalty = penaltyScore;
#endif

			// スコアに寄与する観測点が1点のみの場合は単独ノイズの可能性があるためスキップ
			if (contributingPointCount <= 1)
				continue;

			if (finalScore < threshold)
				continue;

			// この時点で検知扱い
			point.EventedAt = time;

			var uniqueEvents = events.Distinct();
			// 複数件ある場合イベントをマージする
			if (uniqueEvents.Count() > 1)
			{
				// createdAt が一番古いイベントにマージする
				var firstEvent = uniqueEvents.OrderBy(e => e.CreatedAt).First();
				foreach (var evt in uniqueEvents)
				{
					if (evt == firstEvent)
						continue;
					firstEvent.MergeEvent(evt);
					KyoshinEvents.Remove(evt);
					//Logger.LogDebug($"イベント統合: {firstEvent.Id} <- {evt.Id}");
				}

				// マージしたイベントと異なる状態だった場合追加
				if (point.Event == firstEvent)
					continue;
				if (point.Event == null)
					Logger?.LogDebug($"揺れ検知: {point.Code} {firstEvent.Id} スコア:{finalScore:F2} 閾値:{threshold:F2} ペナルティ:{penaltyScore:F2}");
				firstEvent.AddPoint(point, time, Parameters.GetSeconds(KyoshinEvent.GetLevel(point.LatestIntensity)));
				UpdateEventConfirmation(firstEvent);
				continue;
			}
			// 1件の場合はイベントに追加
			if (uniqueEvents.Any())
			{
				if (point.Event == null)
					Logger?.LogDebug($"揺れ検知: {point.Code} {events[0].Id} スコア:{finalScore:F2} 閾値:{threshold:F2} ペナルティ:{penaltyScore:F2}");
				events[0].AddPoint(point, time, Parameters.GetSeconds(KyoshinEvent.GetLevel(point.LatestIntensity)));
				UpdateEventConfirmation(events[0]);
				continue;
			}

			// 存在しなかった場合はイベント作成
			if (point.Event == null)
			{
				var level = KyoshinEvent.GetLevel(point.LatestIntensity);
				point.Event = new(time, point, Parameters.GetSeconds(level));
				KyoshinEvents.Add(point.Event);
				UpdateEventConfirmation(point.Event);
				Logger?.LogDebug($"揺れ検知(新規): {point.Code} {point.Event.Id} スコア:{finalScore:F2} 閾値:{threshold:F2} ペナルティ:{penaltyScore:F2}");
			}
		}

		// イベントの紐づけ
		foreach (var evt in KyoshinEvents.OrderBy(e => e.CreatedAt).ToArray())
		{
			if (!KyoshinEvents.Contains(evt))
				continue;

			// 2つのイベントが 一定距離未満の場合マージする（より強い揺れの統合距離を使用）
			foreach (var evt2 in KyoshinEvents.Where(e => e != evt && evt.CreatedAt <= e.CreatedAt).ToArray())
			{
				var mergeDistance = Parameters.GetMergeDistance(evt.Level > evt2.Level ? evt.Level : evt2.Level);
				if (!evt.CheckNearby(evt2, mergeDistance))
					continue;
				evt.MergeEvent(evt2);
				KyoshinEvents.Remove(evt2);
				Logger?.LogDebug($"イベント距離統合: {evt.Id} <- {evt2.Id}");
			}
		}
	}

	/// <summary>
	/// イベントの確定状態を更新する
	/// 離島でない場合: Weaker, Weak なら指定観測点数以上、より大きいレベルなら確定
	/// </summary>
	private void UpdateEventConfirmation(KyoshinEvent evt)
	{
		if (evt.IsConfirmed)
			return;
		if ((evt.Level <= KyoshinEventLevel.Weaker && evt.PointCount > Parameters.WeakerConfirmPointCount) ||
			(evt.Level == KyoshinEventLevel.Weak && evt.PointCount > Parameters.WeakConfirmPointCount) ||
			evt.Level > KyoshinEventLevel.Weak)
			evt.IsConfirmed = true;
	}

	/// <summary>
	/// 履歴をリセットする
	/// </summary>
	public void ResetHistories()
	{
		if (Points == null)
			return;
		foreach (var point in Points)
		{
			point.ResetHistory();
			point.Event = null;
			point.EventedExpireAt = DateTime.MinValue;
		}
		KyoshinEvents.Clear();
	}
}
