using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Services.Workflows;
using KyoshinMonitorLib;
using System;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Workflow;

public class EewEvent(KyoshinMonitorSeries? series, EewEventType subType) : WorkflowEvent("Eew", series)
{
	public EewEventType EventSubType { get; init; } = subType;

	public DateTime? OccurrenceAt { get; init; }

	public required string EewId { get; init; }
	public required string EewSource { get; init; }

	public int SerialNo { get; init; }

	public bool IsTrueCancelled { get; init; }

	public JmaIntensity Intensity { get; init; }
	public string IntensityLongName => Intensity.ToLongString();
	public bool IsIntensityOver { get; init; }

	public string? EpicenterPlaceName { get; init; }
	public Location? EpicenterLocation { get; init; }
	public float? Magnitude { get; init; }
	public int? Depth { get; init; }

	public bool? IsTemporaryEpicenter { get; init; }

	public bool IsWarning { get; init; }
	public int[]? WarningAreaCodes { get; init; }
	public string[]? WarningAreaNames { get; init; }

	public bool IsFinal { get; init; }
	public bool IsCancelled { get; init; }

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
