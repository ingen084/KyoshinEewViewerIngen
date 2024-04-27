using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Services.Workflows;
using KyoshinMonitorLib;
using System;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Workflow;

public class EewEvent(EewEventType subType) : WorkflowEvent("Eew")
{
	public EewEventType EventSubType { get; init; } = subType;

	public DateTime OccurrenceAt { get; init; }

	public string EewId { get; init; }
	public string EewSource { get; init; }

	public int Serial { get; init; }

	public bool IsTrueCancelled { get; init; }

	public JmaIntensity Intensity { get; init; }
	public bool IsIntensityOver { get; init; }

	public string? EpicenterPlaceName { get; init; }
	public Location? EpicenterLocation { get; init; }
	public float? Magnitude { get; init; }
	public int Depth { get; init; }

	public bool IsTemporaryEpicenter { get; init; }

	public bool IsWarning { get; init; }
	public int[]? WarningAreaCodes { get; init; }
	public string[]? WarningAreaNames { get; init; }

	public static EewEvent FromEewModel(EewEventType type, IEew eew)
		=> new(type)
		{
			OccurrenceAt = eew.OccurrenceTime,
			EewId = eew.Id,
			EewSource = eew.SourceDisplay,
			Serial = eew.Count,
			IsTrueCancelled = eew.IsTrueCancelled,
			Intensity = eew.Intensity,
			IsIntensityOver = eew.IsIntensityOver,
			EpicenterPlaceName = eew.Place,
			EpicenterLocation = eew.Location,
			Magnitude = eew.Magnitude,
			Depth = eew.Depth,
			IsTemporaryEpicenter = eew.IsTemporaryEpicenter,
			IsWarning = eew.IsWarning,
			WarningAreaCodes = eew.WarningAreaCodes,
			WarningAreaNames = eew.WarningAreaNames,
		};
}

public enum EewEventType
{
	New,
	NewWarning,
	UpdateNewSerial,
	UpdateWithMoreAccurate,
	Final,
	Cancel,
}
