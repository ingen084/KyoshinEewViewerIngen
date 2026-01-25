using Avalonia.Controls;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.KyoshinMonitorObservationPoint;
using KyoshinEewViewer.Core.ShakeDetection;
using KyoshinEewViewer.Services.Workflows;
using ReactiveUI;
using System;
using System.Collections.Generic;

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

public class ShakeDetectTrigger : WorkflowTrigger
{
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

	public override Control DisplayControl => new ShakeDetectTriggerControl() { DataContext = this };

	private KyoshinEventLevel _level = KyoshinEventLevel.Medium;
	public KyoshinEventLevel Level
	{
		get => _level;
		set => this.RaiseAndSetIfChanged(ref _level, value);
	}

	private bool _isExact = false;
	public bool IsExact
	{
		get => _isExact;
		set => this.RaiseAndSetIfChanged(ref _isExact, value);
	}

	private PeakRegionExpansionMode _peakRegionExpansionMode = PeakRegionExpansionMode.None;
	/// <summary>
	/// 最大レベル地域拡大時のトリガー発火モード
	/// </summary>
	public PeakRegionExpansionMode PeakRegionExpansionMode
	{
		get => _peakRegionExpansionMode;
		set => this.RaiseAndSetIfChanged(ref _peakRegionExpansionMode, value);
	}

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
			false
		)
		{
			IsTest = true,
		};
	}
}
