using SkiaSharp;
using System;
using System.Collections.Generic;

namespace KyoshinEewViewer.Core.Models;

public class KyoshinEvent
{
	public KyoshinEvent(DateTime createdAt, RealtimeObservationPoint firstPoint)
	{
		CreatedAt = createdAt;
		Points.Add(firstPoint);
		DebugColor = ColorCycle[CycleCount++];
		if (CycleCount >= ColorCycle.Length)
			CycleCount = 0;
	}
	public DateTime CreatedAt { get; }
	public List<RealtimeObservationPoint> Points { get; } = new();

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
