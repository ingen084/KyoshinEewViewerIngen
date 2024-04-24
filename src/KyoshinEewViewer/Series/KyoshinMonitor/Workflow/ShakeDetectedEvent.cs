using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Services.Workflows;
using System;
using System.Linq;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Workflow;

public class ShakeDetectedEvent(DateTime time, KyoshinEvent evt) : WorkflowEvent("KyoshinShakeDetected")
{
	public DateTime EventedAt { get; } = time;
	public DateTime FirstEventedAt { get; } = evt.CreatedAt;
	public KyoshinEventLevel Level { get; } = evt.Level;
	public Guid KyoshinEventId { get; } = evt.Id;
	public string[] Regions { get; } = evt.Points.Select(p => p.Region).Distinct().ToArray();
}
