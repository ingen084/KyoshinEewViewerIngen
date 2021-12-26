using KyoshinMonitorLib;
using SkiaSharp;
using System;

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
	/// 地理座標(日本座標系)
	/// </summary>
	public Location? OldLocation { get; }

	/// <summary>
	/// 強震モニタ画像上での座標
	/// </summary>
	public SKPointI ImageLocation { get; set; }

	/// <summary>
	/// 有効な状態にあるか
	/// </summary>
	public bool IsValid { get; set; }

	/// <summary>
	/// 観測点の色
	/// </summary>
	public SKColor? LatestColor { get; set; }

	/// <summary>
	/// 最新のリアルタイム震度値
	/// </summary>
	public double? LatestIntensity { get; set; }

	public RealtimeObservationPoint(ObservationPoint basePoint)
	{
		Type = basePoint.Type;
		Code = basePoint.Code;
		Name = basePoint.Name;
		Region = basePoint.Region;
		IsSuspended = basePoint.IsSuspended;
		Location = basePoint.Location;
		OldLocation = basePoint.OldLocation;
		if (basePoint.Point is not Point2 p)
			throw new ArgumentNullException("basePoint.Point");
		ImageLocation = new(p.X, p.Y);
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
		if (LatestColor is null || LatestIntensity is null)
			IsValid = false;
	}
}
