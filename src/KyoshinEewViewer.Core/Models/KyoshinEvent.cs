using KyoshinMonitorLib;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Core.Models;

public class KyoshinEvent
{
	public Guid Id { get; }
	public KyoshinEvent(DateTime createdAt, RealtimeObservationPoint firstPoint)
	{
		Id = Guid.NewGuid();
		CreatedAt = createdAt;
		firstPoint.EventedAt = createdAt;
		_points.Add(firstPoint);
		Level = GetLevel(firstPoint.LatestIntensity);
		var eex = createdAt.AddSeconds(GetSeconds(Level));
		if (firstPoint.EventedExpireAt < eex)
			firstPoint.EventedExpireAt = eex;
		DebugColor = ColorCycle[CycleCount++];
		TopLeft = new(firstPoint.Location.Latitude, firstPoint.Location.Longitude);
		BottomRight = new(firstPoint.Location.Latitude, firstPoint.Location.Longitude);
		if (CycleCount >= ColorCycle.Length)
			CycleCount = 0;
	}
	public KyoshinEventLevel Level { get; set; }
	public DateTime CreatedAt { get; }
	public Location TopLeft { get; }
	public Location BottomRight { get; }
	public int PointCount => _points.Count;

	private readonly List<RealtimeObservationPoint> _points = [];
	public IReadOnlyList<RealtimeObservationPoint> Points => _points;

	public void AddPoint(RealtimeObservationPoint point, DateTime time)
	{
		var lv = GetLevel(point.LatestIntensity);
		if (Level < lv)
			Level = lv;
		point.EventedAt = time;
		var eex = time.AddSeconds(GetSeconds(lv));
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
			Level = evt.Level;
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
	public bool CheckNearby(KyoshinEvent evt)
		=> _points.Any(p1 => evt._points.Any(p2 => p1.Location.Distance(p2.Location) <= 250));
	public static KyoshinEventLevel GetLevel(double? intensity)
		=> intensity switch
		{
			> 4.5 => KyoshinEventLevel.Stronger,
			> 2.5 => KyoshinEventLevel.Strong,
			> 0.5 => KyoshinEventLevel.Medium,
			> -1 => KyoshinEventLevel.Weak,
			_ => KyoshinEventLevel.Weaker,
		};
	public static int GetSeconds(KyoshinEventLevel level)
		=> level switch
		{
			KyoshinEventLevel.Stronger => 90,
			KyoshinEventLevel.Strong => 60,
			KyoshinEventLevel.Medium => 30,
			KyoshinEventLevel.Weak => 15,
			_ => 10,
		};

	public SKColor DebugColor { get; }

	private static int CycleCount { get; set; } = 0;
	private static SKColor[] ColorCycle { get; } = [
		new SKColor(200, 0, 0, 100),
		new SKColor(0, 255, 0, 100),
		new SKColor(255, 0, 255, 100),
		new SKColor(0xda, 0xa5, 0x20, 100),
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
