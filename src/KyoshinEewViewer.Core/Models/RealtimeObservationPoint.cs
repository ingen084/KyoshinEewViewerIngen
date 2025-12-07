using KyoshinEewViewer.Core.Models.KyoshinMonitorObservationPoint;
using KyoshinMonitorLib;
using SkiaSharp;
using System;
using System.Linq;

namespace KyoshinEewViewer.Core.Models;

public class RealtimeObservationPoint
{
	/// <summary>
	/// 観測地点のネットワークの種類
	/// </summary>
	public ObservationPointType Type { get; }

	/// <summary>
	/// 観測点コード
	/// </summary>
	public string Code { get; }

	/// <summary>
	/// 観測点名
	/// </summary>
	public string Name { get; }

	/// <summary>
	/// 観測点広域名
	/// </summary>
	public string Region { get; }

	/// <summary>
	/// 観測地点が休止状態(無効)かどうか
	/// </summary>
	public bool IsSuspended { get; }

	/// <summary>
	/// 地理座標
	/// </summary>
	public Location Location { get; }

	/// <summary>
	/// 強震モニタ画像上での座標
	/// </summary>
	public SKPointI ImageLocation { get; set; }

	/// <summary>
	/// 有効な状態にあるか
	/// </summary>
	public bool IsValid => LatestColor is null || LatestIntensity is null;

	/// <summary>
	/// 観測点の色
	/// </summary>
	public SKColor? LatestColor { get; set; }

	private const int IntensityHistoryCount = 10;
	private int IntensityHistoryPosition { get; set; } = 0;
	private double?[] IntensityHistory { get; } = new double?[IntensityHistoryCount];

	/// <summary>
	/// 最新のリアルタイム震度値
	/// </summary>
	public double? LatestIntensity
	{
		get => IntensityHistory[IntensityHistoryPosition];
		set {
			if (IntensityHistoryPosition + 1 >= IntensityHistoryCount)
				IntensityHistoryPosition = 0;
			else
				IntensityHistoryPosition++;
			IntensityHistory[IntensityHistoryPosition] = value;

			// 上昇値を計算
			double? before = null;
			var diff = 0d;
			var total = 0d;
			for (var i = IntensityHistoryPosition; i >= 0; i--)
			{
				if (IntensityHistory[i] is { } intensity)
				{
					if (before is { } beforeValue)
						diff += beforeValue - intensity;
					before = intensity;
					total += intensity;
				}
			}
			for (var i = IntensityHistoryCount - 1; i > IntensityHistoryPosition; i--)
			{
				if (IntensityHistory[i] is { } intensity)
				{
					if (before is { } beforeValue)
						diff += beforeValue - intensity;
					before = intensity;
					total += intensity;
				}
			}
			IntensityDiff = diff;
			IntensityAverage = total / IntensityHistoryCount;
		}
	}

	public double IntensityDiff { get; private set; }
	public double IntensityAverage { get; private set; }
	public bool IsTmpDisabled { get; set; }

	/// <summary>
	/// 紐づいた強震イベント
	/// </summary>
	public KyoshinEvent? Event { get; set; }
	public DateTime EventedAt { get; set; }
	public DateTime EventedExpireAt { get; set; }

	/// <summary>
	/// 起動後正常に観測点として取得した履歴が存在するか
	/// </summary>
	public bool HasValidHistory { get; set; }

	/// <summary>
	/// 距離付き近傍観測点
	/// </summary>
	public readonly record struct NearPointInfo(RealtimeObservationPoint Point, double Distance, double Weight);

	private NearPointInfo[]? _nearPoints;
	/// <summary>
	/// 近くの観測点（距離と重み付き）
	/// </summary>
	public NearPointInfo[]? NearPoints
	{
		get => _nearPoints;
		set {
			_nearPoints = value;
			HasNearPoints = _nearPoints?.Length > 0;
			TotalNearPointWeight = _nearPoints?.Sum(p => p.Weight) ?? 0;
		}
	}
	public bool HasNearPoints { get; private set; } = false;

	/// <summary>
	/// 近傍観測点の重みの合計
	/// </summary>
	public double TotalNearPointWeight { get; private set; } = 0;

	public RealtimeObservationPoint(ObservationPointV2 basePoint)
	{
		Type = basePoint.Type;
		Code = basePoint.Code;
		Name = basePoint.Name;
		Region = basePoint.Region;
		IsSuspended = basePoint.IsSuspended;
		Location = basePoint.Location;
		if (basePoint.Point is not { } p)
			throw new ArgumentNullException("basePoint.Point");
		ImageLocation = new SKPointI(p.Center.X + p.Offset.X, p.Center.Y + p.Offset.Y);
	}

	/// <summary>
	/// 観測情報を更新する
	/// </summary>
	/// <param name="color">色</param>
	/// <param name="intensity">リアルタイム震度</param>
	public void Update(SKColor? color, double? intensity)
	{
		LatestColor = color;
		LatestIntensity = intensity;
		if (LatestColor is not null && LatestIntensity is not null)
			HasValidHistory = true;
	}

	/// <summary>
	/// 震度の履歴を削除する
	/// </summary>
	public void ResetHistory()
	{
		for (var i = 0; i < IntensityHistory.Length; i++)
			IntensityHistory[i] = null;
		IntensityHistoryPosition = 0;
	}
}
