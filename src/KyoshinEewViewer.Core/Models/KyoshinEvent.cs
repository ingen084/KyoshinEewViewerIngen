using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Core.Models;

public class KyoshinEvent
{
	public KyoshinEvent(DateTime createdAt, RealtimeObservationPoint firstPoint)
	{
		CreatedAt = createdAt;
		Points.Add(firstPoint);
		Level = GetLevel(firstPoint.LatestIntensity);
		DebugColor = ColorCycle[CycleCount++];
		if (CycleCount >= ColorCycle.Length)
			CycleCount = 0;
	}
	public KyoshinEventLevel Level { get; set; }
	public DateTime CreatedAt { get; }
	private List<RealtimeObservationPoint> Points { get; } = new();
	public int PointCount => Points.Count;

	public void AddPoint(RealtimeObservationPoint point, DateTime time)
	{
		var lv = GetLevel(point.LatestIntensity);
		if (Level < lv)
			Level = lv;
		point.EventedAt = time;
		var eex = time.AddSeconds(GetSeconds(lv));
		if (point.EventedExpireAt < eex)
			point.EventedExpireAt = eex;

		if (Points.Contains(point))
			return;
		point.Event = this;
		Points.Add(point);
	}
	public void MergeEvent(KyoshinEvent evt)
	{
		foreach (var p in evt.Points)
			p.Event = this;
		if (Level < evt.Level)
			Level = evt.Level;
		Points.AddRange(evt.Points);
	}
	public void RemovePoint(RealtimeObservationPoint point)
	{
		if (!Points.Contains(point))
			return;
		point.Event = null;
		Points.Remove(point);
	}
	public bool CheckNearby(KyoshinEvent evt)
		=> Points.Any(p1 => evt.Points.Any(p2 => p1.Location.Distance(p2.Location) <= 120));
	public static KyoshinEventLevel GetLevel(double? intensity)
		=> intensity switch
		{
			> 4.5 => KyoshinEventLevel.Strongest,
			> 2.5 => KyoshinEventLevel.Strong,
			> 0.5 => KyoshinEventLevel.Medium,
			_ => KyoshinEventLevel.Weak,
		};
	public static int GetSeconds(KyoshinEventLevel level)
		=> level switch
		{
			KyoshinEventLevel.Strongest or KyoshinEventLevel.Strong => 90,
			KyoshinEventLevel.Medium => 30,
			_ => 15,
		};

	public SKColor DebugColor { get; }

	private static int CycleCount { get; set; } = 0;
	private static SKColor[] ColorCycle { get; } = new[]
	{
		SKColors.Red,
		SKColors.Blue,
		SKColors.ForestGreen,
		SKColors.Magenta,
	};
}

public enum KyoshinEventLevel
{
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
	Strongest,
}
