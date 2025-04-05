using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Services.Workflows;
using System;
using System.Linq;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Workflow;

public class ShakeDetectedEvent(KyoshinMonitorSeries? series, DateTime time, KyoshinEvent evt, bool isReplay) : WorkflowEvent("KyoshinShakeDetected", series)
{
	public DateTime EventedAt { get; } = time;
	public DateTime FirstEventedAt { get; } = evt.CreatedAt;
	public KyoshinEventLevel Level { get; } = evt.Level;
	public Guid KyoshinEventId { get; } = evt.Id;
	public string[] Regions { get; } = evt.Points.Select(p => p.Region).Distinct().ToArray();
	public bool IsReplay { get; } = isReplay;
}
