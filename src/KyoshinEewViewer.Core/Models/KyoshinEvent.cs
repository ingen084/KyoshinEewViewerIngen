using KyoshinMonitorLib;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Core.Models;

public class KyoshinEvent
{
	public Guid Id { get; }

	public KyoshinEvent(DateTime createdAt, RealtimeObservationPoint firstPoint, int expireSeconds)
	{
		Id = Guid.NewGuid();
		CreatedAt = createdAt;
		firstPoint.EventedAt = createdAt;
		_points.Add(firstPoint);
		Level = GetLevel(firstPoint.LatestIntensity);
		_peakLevelRegions.Add((firstPoint.Region, firstPoint.SubRegion));
		var eex = createdAt.AddSeconds(expireSeconds);
		if (firstPoint.EventedExpireAt < eex)
			firstPoint.EventedExpireAt = eex;
		DebugColor = ColorCycle[CycleCount++ % ColorCycle.Length];
		TopLeft = new(firstPoint.Location.Latitude, firstPoint.Location.Longitude);
		BottomRight = new(firstPoint.Location.Latitude, firstPoint.Location.Longitude);
	}
	public KyoshinEventLevel Level { get; set; }
	public DateTime CreatedAt { get; }
	public Location TopLeft { get; }
	public Location BottomRight { get; }
	public int PointCount => _points.Count;

	/// <summary>
	/// 正式なイベントとして確定しているかどうか
	/// </summary>
	public bool IsConfirmed { get; set; }

	private readonly List<RealtimeObservationPoint> _points = [];
	public IReadOnlyList<RealtimeObservationPoint> Points => _points;

	/// <summary>
	/// 最高レベルを検出した地域のセット（Region, SubRegion のタプル）
	/// </summary>
	private readonly HashSet<(string Region, string? SubRegion)> _peakLevelRegions = [];
	public IReadOnlyCollection<(string Region, string? SubRegion)> PeakLevelRegions => _peakLevelRegions;

	public void AddPoint(RealtimeObservationPoint point, DateTime time, int expireSeconds)
	{
		var lv = GetLevel(point.LatestIntensity);
		// 1点のみの超過ではノイズの可能性があるためレベルアップしない
		// 同じレベル以上の観測点が既に1点以上ある場合のみレベルアップ
		if (Level < lv && _points.Any(p => GetLevel(p.LatestIntensity) >= lv))
		{
			Level = lv;
			// レベルアップ時は最高レベル地域リストをクリアして新しいレベルの地域を追加
			_peakLevelRegions.Clear();
		}

		// 現在の最高レベルと同じレベルの観測点の地域を記録
		if (lv == Level)
			_peakLevelRegions.Add((point.Region, point.SubRegion));

		point.EventedAt = time;
		var eex = time.AddSeconds(expireSeconds);
		if (point.EventedExpireAt < eex)
			point.EventedExpireAt = eex;

		if (_points.Contains(point))
			return;
		if (TopLeft.Latitude > point.Location.Latitude)
			TopLeft.Latitude = point.Location.Latitude;
		if (TopLeft.Longitude > point.Location.Longitude)
			TopLeft.Longitude = point.Location.Longitude;
		if (BottomRight.Latitude < point.Location.Latitude)
			BottomRight.Latitude = point.Location.Latitude;
		if (BottomRight.Longitude < point.Location.Longitude)
			BottomRight.Longitude = point.Location.Longitude;
		point.Event = this;
		_points.Add(point);
	}
	public void MergeEvent(KyoshinEvent evt)
	{
		foreach (var p in evt._points)
			p.Event = this;
		if (Level < evt.Level)
		{
			Level = evt.Level;
			// マージ先のレベルが高い場合は、そちらの最高レベル地域を使用
			_peakLevelRegions.Clear();
			_peakLevelRegions.UnionWith(evt._peakLevelRegions);
		}
		else if (Level == evt.Level)
		{
			// 同じレベルの場合は最高レベル地域をマージ
			_peakLevelRegions.UnionWith(evt._peakLevelRegions);
		}
		// Level > evt.Level の場合は何もしない（現在の最高レベル地域を維持）

		if (TopLeft.Latitude > evt.TopLeft.Latitude)
			TopLeft.Latitude = evt.TopLeft.Latitude;
		if (TopLeft.Longitude > evt.TopLeft.Longitude)
			TopLeft.Longitude = evt.TopLeft.Longitude;
		if (BottomRight.Latitude < evt.BottomRight.Latitude)
			BottomRight.Latitude = evt.BottomRight.Latitude;
		if (BottomRight.Longitude < evt.BottomRight.Longitude)
			BottomRight.Longitude = evt.BottomRight.Longitude;
		_points.AddRange(evt._points);
	}
	public void RemovePoint(RealtimeObservationPoint point)
	{
		if (!_points.Contains(point))
			return;
		point.Event = null;
		point.EventedExpireAt = DateTime.MinValue;
		_points.Remove(point);
	}

	public bool CheckNearby(KyoshinEvent evt, double distance)
		=> _points.Any(p1 => evt._points.Any(p2 => p1.Location.Distance(p2.Location) <= distance));
	public static KyoshinEventLevel GetLevel(double? intensity)
		=> intensity switch
		{
			> 4.5 => KyoshinEventLevel.Stronger,
			> 2.5 => KyoshinEventLevel.Strong,
			> 0.5 => KyoshinEventLevel.Medium,
			> -1 => KyoshinEventLevel.Weak,
			_ => KyoshinEventLevel.Weaker,
		};
		
	public SKColor DebugColor { get; }

	private static int CycleCount { get; set; } = 0;
	private static SKColor[] ColorCycle { get; } = [
		new SKColor(200, 0, 0, 200),
		new SKColor(0, 255, 0, 200),
		new SKColor(255, 0, 255, 200),
		new SKColor(0xda, 0xa5, 0x20, 200),
	];
}

public enum KyoshinEventLevel
{
	/// <summary>
	/// 震度-0.5未満の揺れ
	/// </summary>
	Weaker,
	/// <summary>
	/// 震度1未満の揺れ
	/// </summary>
	Weak,
	/// <summary>
	/// 震度2以下の揺れ
	/// </summary>
	Medium,
	/// <summary>
	/// 震度3以上の揺れ
	/// </summary>
	Strong,
	/// <summary>
	/// 震度5弱以上の揺れ
	/// </summary>
	Stronger,
}
