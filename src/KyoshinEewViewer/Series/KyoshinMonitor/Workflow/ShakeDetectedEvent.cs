using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Services.Workflows;
using System;
using System.ComponentModel;
using System.Linq;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Workflow;

public class ShakeDetectedEvent(KyoshinMonitorSeries? series, DateTime time, KyoshinEvent evt, bool isReplay, bool isRegionExpanded, bool isSubRegionExpanded) : WorkflowEvent("KyoshinShakeDetected", series)
{
	[Description("揺れ検知時刻 (現在のイベント時点)")]
	public DateTime EventedAt { get; } = time;

	[Description("最初に揺れを検知した時刻")]
	public DateTime FirstEventedAt { get; } = evt.CreatedAt;

	[Description("検知レベル (Weaker, Weak, Medium, Strong, Stronger)")]
	public KyoshinEventLevel Level { get; } = evt.Level;

	[Description("検知イベントの一意 ID")]
	public Guid KyoshinEventId { get; } = evt.Id;

	[Description("揺れを検知した地域名の配列")]
	public string[] Regions { get; } = evt.Points.Select(p => p.Region).Distinct().ToArray();

	[Description("最大レベルを検出した地域とそのサブ地域の配列")]
	public RegionInfo[] RegionDetails { get; } = evt.PeakLevelRegions
		.GroupBy(r => r.Region)
		.Select(g => new RegionInfo(
			g.Key,
			g.Select(r => r.SubRegion).Where(s => s != null).Distinct().ToArray()!))
		.ToArray();

	[Description("リプレイ中に発生したイベントかどうか")]
	public bool IsReplay { get; } = isReplay;

	[Description("地域 (Region) が拡大したイベントかどうか")]
	public bool IsRegionExpanded { get; } = isRegionExpanded;

	[Description("サブ地域 (Region+SubRegion) が拡大したイベントかどうか")]
	public bool IsSubRegionExpanded { get; } = isSubRegionExpanded;
}

/// <summary>
/// 地域情報（階層構造）
/// </summary>
/// <param name="Name">地域名</param>
/// <param name="SubRegions">サブ地域名の配列</param>
public record RegionInfo(
	[property: Description("地域名")] string Name,
	[property: Description("サブ地域名の配列")] string[] SubRegions);
