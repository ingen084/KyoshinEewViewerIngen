using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Services.Workflows;
using System;
using System.ComponentModel;
using System.Linq;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Workflow;

public class ShakeDetectedEvent(
	KyoshinMonitorSeries? series,
	DateTime time,
	KyoshinEvent evt,
	bool isReplay,
	bool isRegionExpanded,
	bool isSubRegionExpanded,
	ShakeDetectedRegion[] regionDetails
) : WorkflowEvent("KyoshinShakeDetected", series)
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
	public ShakeDetectedRegion[] RegionDetails { get; } = regionDetails;

	[Description("リプレイ中に発生したイベントかどうか")]
	public bool IsReplay { get; } = isReplay;

	[Description("地域 (Region) が拡大したイベントかどうか")]
	public bool IsRegionExpanded { get; } = isRegionExpanded;

	[Description("サブ地域 (Region+SubRegion) が拡大したイベントかどうか")]
	public bool IsSubRegionExpanded { get; } = isSubRegionExpanded;
}
