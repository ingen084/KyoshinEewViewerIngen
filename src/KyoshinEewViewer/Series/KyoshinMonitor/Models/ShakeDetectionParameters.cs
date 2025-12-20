using KyoshinEewViewer.Core.Models;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Models;

/// <summary>
/// 揺れ検知のパラメータ
/// </summary>
public record class ShakeDetectionParameters
{
	/// <summary>
	/// 最大探索距離 (km)
	/// </summary>
	public double MaxSearchDistance { get; init; } = 60.0;

	/// <summary>
	/// 最大重みとなる距離 (km)
	/// </summary>
	public double PeakWeightDistance { get; init; } = 14.0;

	/// <summary>
	/// 離島判定閾値 (重み合計がこの値未満で離島扱い)
	/// </summary>
	public double IsolatedThreshold { get; init; } = 1;

	/// <summary>
	/// 単独検知変位閾値 (離島での検知に必要な最小変位)
	/// </summary>
	public double IsolatedDetectionDiff { get; init; } = 1.4;

	/// <summary>
	/// 検知スコア閾値の係数 (有効重み合計に対する比率)
	/// </summary>
	public double ScoreThresholdRatio { get; init; } = 0.333333;

	/// <summary>
	/// スコア計算時の震度上昇オフセット
	/// </summary>
	public double ScoreIntensityOffset { get; init; } = -0.8;

	/// <summary>
	/// 検知に必要な最小震度上昇量
	/// </summary>
	public double MinDetectionDiff { get; init; } = 0.2;

	/// <summary>
	/// 近傍観測点が無反応の場合のペナルティ係数
	/// 近い観測点が反応していない場合にスコアから減算される
	/// </summary>
	public double NoChangePenaltyFactor { get; init; } = 0.25;

	/// <summary>
	/// 近傍観測点の最大取得数
	/// </summary>
	public int MaxNearPoints { get; init; } = 50;

	/// <summary>
	/// イベント統合距離: Stronger (km)
	/// </summary>
	public double StrongerMergeDistance { get; init; } = 250.0;

	/// <summary>
	/// イベント統合距離: Strong (km)
	/// </summary>
	public double StrongMergeDistance { get; init; } = 250.0;

	/// <summary>
	/// イベント統合距離: Medium (km)
	/// </summary>
	public double MediumMergeDistance { get; init; } = 180.0;

	/// <summary>
	/// イベント統合距離: Weak (km)
	/// </summary>
	public double WeakMergeDistance { get; init; } = 130.0;

	/// <summary>
	/// イベント統合距離: Weaker (km)
	/// </summary>
	public double WeakerMergeDistance { get; init; } = 90.0;

	/// <summary>
	/// 有効時間: Stronger (秒)
	/// </summary>
	public int StrongerSeconds { get; init; } = 80;

	/// <summary>
	/// 有効時間: Strong (秒)
	/// </summary>
	public int StrongSeconds { get; init; } = 70;

	/// <summary>
	/// 有効時間: Medium (秒)
	/// </summary>
	public int MediumSeconds { get; init; } = 40;

	/// <summary>
	/// 有効時間: Weak (秒)
	/// </summary>
	public int WeakSeconds { get; init; } = 30;

	/// <summary>
	/// 有効時間: Weaker (秒)
	/// </summary>
	public int WeakerSeconds { get; init; } = 20;

	/// <summary>
	/// イベント確定に必要な観測点数: Weaker
	/// この値より大きい観測点数で確定とみなす
	/// </summary>
	public int WeakerConfirmPointCount { get; init; } = 4;

	/// <summary>
	/// イベント確定に必要な観測点数: Weak
	/// この値より大きい観測点数で確定とみなす
	/// </summary>
	public int WeakConfirmPointCount { get; init; } = 3;

	/// <summary>
	/// イベントレベルに応じた有効時間を取得する
	/// </summary>
	public int GetSeconds(KyoshinEventLevel level)
		=> level switch
		{
			KyoshinEventLevel.Stronger => StrongerSeconds,
			KyoshinEventLevel.Strong => StrongSeconds,
			KyoshinEventLevel.Medium => MediumSeconds,
			KyoshinEventLevel.Weak => WeakSeconds,
			_ => WeakerSeconds,
		};

	/// <summary>
	/// イベントレベルに応じた統合距離を取得する
	/// </summary>
	public double GetMergeDistance(KyoshinEventLevel level)
		=> level switch
		{
			KyoshinEventLevel.Stronger => StrongerMergeDistance,
			KyoshinEventLevel.Strong => StrongMergeDistance,
			KyoshinEventLevel.Medium => MediumMergeDistance,
			KyoshinEventLevel.Weak => WeakMergeDistance,
			_ => WeakerMergeDistance,
		};

	/// <summary>
	/// 距離に基づく重みを計算する
	/// 近すぎる観測点（生活振動の影響）と遠すぎる観測点（相関低下）の重みを下げる
	/// </summary>
	/// <param name="distance">距離（km）</param>
	/// <returns>重み（0.0〜1.0）</returns>
	public double CalculateDistanceWeight(double distance)
	{
		if (distance <= PeakWeightDistance)
			// 0km → 0, PeakWeightDistance → 1.0
			return distance / PeakWeightDistance;
		// PeakWeightDistance → 1.0, MaxSearchDistance → 0
		return (MaxSearchDistance - distance) / (MaxSearchDistance - PeakWeightDistance);
	}

	/// <summary>
	/// デフォルトのパラメータを取得する
	/// </summary>
	public static ShakeDetectionParameters Default => new();
}
