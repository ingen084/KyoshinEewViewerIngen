using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.KyoshinMonitorObservationPoint;
using KyoshinEewViewer.Core.ShakeDetection;
using KyoshinEewViewer.Services.Workflows;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Workflow;

/// <summary>
/// 最大レベル地域拡大時のトリガー発火モード
/// </summary>
public enum PeakRegionExpansionMode
{
	/// <summary>
	/// 地域拡大時にトリガーを発火しない
	/// </summary>
	None,
	/// <summary>
	/// 地域（Region）が増加した場合のみ発火
	/// </summary>
	RegionOnly,
	/// <summary>
	/// サブ地域（SubRegion）が増加した場合も発火
	/// </summary>
	IncludeSubRegion,
}

public partial class ShakeDetectTrigger : WorkflowTrigger
{
	public override Type EventType => typeof(ShakeDetectedEvent);

	public static Dictionary<KyoshinEventLevel, string> LevelNames { get; } = new()
	{
		{ KyoshinEventLevel.Weaker, "微弱(非推奨)" },
		{ KyoshinEventLevel.Weak, "弱い(震度1未満)" },
		{ KyoshinEventLevel.Medium, "普通(震度1程度以上)" },
		{ KyoshinEventLevel.Strong, "強い(震度3程度以上)" },
		{ KyoshinEventLevel.Stronger, "非常に強い(震度5弱程度以上)" },
	};

	public static Dictionary<PeakRegionExpansionMode, string> PeakRegionExpansionModeNames { get; } = new()
	{
		{ PeakRegionExpansionMode.None, "なにもしない" },
		{ PeakRegionExpansionMode.RegionOnly, "地域のみ" },
		{ PeakRegionExpansionMode.IncludeSubRegion, "サブ地域も含む" },
	};

	[JsonIgnore]
	public override Control DisplayControl => new ShakeDetectTriggerControl() { DataContext = this };

	[ObservableProperty]
	public partial KyoshinEventLevel Level { get; set; } = KyoshinEventLevel.Medium;

	[ObservableProperty]
	public partial bool IsExact { get; set; } = false;

	/// <summary>
	/// 最大レベル地域拡大時のトリガー発火モード
	/// </summary>
	[ObservableProperty]
	public partial PeakRegionExpansionMode PeakRegionExpansionMode { get; set; } = PeakRegionExpansionMode.None;

	public override bool CheckTrigger(WorkflowEvent content)
	{
		if (content is not ShakeDetectedEvent shakeEvent)
			return false;

		// 地域拡大イベントの場合、PeakRegionExpansionModeに応じて判定
		if (shakeEvent.IsRegionExpanded || shakeEvent.IsSubRegionExpanded)
		{
			// 地域拡大を無視する設定の場合はトリガーしない
			if (PeakRegionExpansionMode == PeakRegionExpansionMode.None)
				return false;

			// RegionOnlyモードでは地域のみの拡大を検知
			if (PeakRegionExpansionMode == PeakRegionExpansionMode.RegionOnly && !shakeEvent.IsRegionExpanded)
				return false;

			// レベル条件を満たすかチェック
			if (IsExact)
				return shakeEvent.Level == Level;

			return shakeEvent.Level >= Level;
		}

		// 通常のイベント（初回検知・レベル上昇）
		if (IsExact)
			return shakeEvent.Level == Level;

		return shakeEvent.Level >= Level;
	}

	public override WorkflowEvent CreateTestEvent()
	{
		var random = new Random();
		var level = IsExact ? Level : random.Next(KyoshinEventLevel.Stronger - Level) + Level;
		return new ShakeDetectedEvent(
			null,
			DateTime.Now,
			new KyoshinEvent(DateTime.Now.AddSeconds(-random.Next(60)),
				new RealtimeObservationPoint(
					new ObservationPointV2()
					{
						Code = "TEST",
						Name = "テスト",
						IsSuspended = false,
						Location = new(0, 0),
						Point = new(new(), new()),
						Region = "テスト県",
						SubRegion = "テスト地方",
						Type = random.Next() % 2 == 0 ? KyoshinMonitorLib.ObservationPointType.KiK_net : KyoshinMonitorLib.ObservationPointType.K_NET,
					}
				),
				ShakeDetectionParameters.Default.GetSeconds(level)
			)
			{
				Level = level,
			},
			random.Next() % 2 == 0,
			false,
			false,
			[]
		)
		{
			IsTest = true,
		};
	}
}
