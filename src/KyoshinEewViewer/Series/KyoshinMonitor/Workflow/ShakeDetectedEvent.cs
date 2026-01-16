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
	// 最高レベルを検出した地域のみを返す
	public RegionInfo[] RegionDetails { get; } = evt.PeakLevelRegions
		.GroupBy(r => r.Region)
		.Select(g => new RegionInfo(
			g.Key,
			g.Select(r => r.SubRegion).Where(s => s != null).Distinct().ToArray()!))
		.ToArray();
	public bool IsReplay { get; } = isReplay;
}

/// <summary>
/// 地域情報（階層構造）
/// </summary>
/// <param name="Name">地域名</param>
/// <param name="SubRegions">サブ地域名の配列</param>
public record RegionInfo(string Name, string[] SubRegions);
