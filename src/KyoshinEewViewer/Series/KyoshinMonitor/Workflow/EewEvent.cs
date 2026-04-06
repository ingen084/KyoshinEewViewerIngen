using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Services.Workflows;
using KyoshinMonitorLib;
using System;
using System.ComponentModel;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Workflow;

public class EewEvent(KyoshinMonitorSeries? series, EewEventType subType) : WorkflowEvent("Eew", series)
{
	[Description("EEW イベント種別 (New, UpdateNewSerial, UpdateWithMoreAccurate, Final, Cancel, NewWarning, UpdateWarning, CancelWarning, WarningLevelReached, IncreaseMaxIntensity, DecreaseMaxIntensity)")]
	public EewEventType EventSubType { get; init; } = subType;

	[Description("地震発生時刻")]
	public DateTime? OccurrenceAt { get; init; }

	[Description("イベントID")]
	public required string EewId { get; init; }

	[Description("緊急地震速報の発表元 (強震モニタ / DM-D.S.S(大阪管区気象台) など)")]
	public required string EewSource { get; init; }

	[Description("報番号 (1始まり)")]
	public int SerialNo { get; init; }

	[Description("確実にキャンセルされたか（強震モニタ経由で擬似的にキャンセルした場合は false になります）")]
	public bool IsTrueCancelled { get; init; }

	[Description("最大予測震度 (Unknown, Int0, Int1, Int2, Int3, Int4, Int5Lower, Int5Upper, Int6Lower, Int6Upper, Int7)")]
	public JmaIntensity Intensity { get; init; }

	[Description("最大予測震度の長い表記 (例: \"震度5強\")")]
	public string IntensityLongName => Intensity.ToLongString();

	[Description("予測震度が「程度以上」かどうか")]
	public bool IsIntensityOver { get; init; }

	[Description("震央地名")]
	public string? EpicenterPlaceName { get; init; }

	[Description("震央位置 (緯度経度)")]
	public Location? EpicenterLocation { get; init; }

	[Description("マグニチュード")]
	public float? Magnitude { get; init; }

	[Description("震源の深さ (km)")]
	public int? Depth { get; init; }

	[Description("仮定震源かどうか (PLUM 法)")]
	public bool? IsTemporaryEpicenter { get; init; }

	[Description("警報かどうか")]
	public bool IsWarning { get; init; }

	[Description("警報対象地域コード配列")]
	public int[]? WarningAreaCodes { get; init; }

	[Description("警報対象地域名配列")]
	public string[]? WarningAreaNames { get; init; }

	[Description("最終報かどうか")]
	public bool IsFinal { get; init; }

	[Description("キャンセルされているかどうか")]
	public bool IsCancelled { get; init; }

	[Description("リプレイ中に発生したイベントかどうか")]
	public bool IsReplay { get; init; }

	public static EewEvent FromEewModel(KyoshinMonitorSeries series, EewEventType type, Eew eew, bool isReplay)
		=> new(series, type)
		{
			OccurrenceAt = eew.Hypocenter?.OccurrenceTime,
			EewId = eew.Id,
			EewSource = eew.DisplaySource,
			SerialNo = eew.SerialNo,
			IsTrueCancelled = eew.IsTrueCancelled,
			Intensity = eew.MaxIntensity,
			IsIntensityOver = eew.IsIntensityOver,
			EpicenterPlaceName = eew.Hypocenter?.Place,
			EpicenterLocation = eew.Hypocenter?.Location,
			Magnitude = eew.Hypocenter?.Magnitude,
			Depth = eew.Hypocenter?.Depth,
			IsTemporaryEpicenter = eew.Hypocenter?.IsTemporary,
			IsWarning = eew.IsWarning,
			WarningAreaCodes = eew.WarningAreas?.Codes,
			WarningAreaNames = eew.WarningAreas?.Names,
			IsFinal = eew.IsFinal,
			IsCancelled = eew.IsCancelled,
			IsReplay = isReplay,
		};
}

public enum EewEventType
{
	New,
	UpdateNewSerial,
	UpdateWithMoreAccurate,
	Final,
	Cancel,
	NewWarning,
	UpdateWarning,
	CancelWarning,
	WarningLevelReached,
	IncreaseMaxIntensity,
	DecreaseMaxIntensity,
}
