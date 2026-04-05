using KyoshinEewViewer.Core;
using KyoshinEewViewer.Series.Earthquake.Models;
using KyoshinEewViewer.Services.Workflows;
using KyoshinMonitorLib;
using System;

namespace KyoshinEewViewer.Series.Earthquake.Workflow;

public class EarthquakeInformationEvent(EarthquakeSeries? series) : WorkflowEvent("EarthquakeInformation", series)
{
	public DateTime UpdatedAt { get; init; }
	public required string LatestInformationName { get; init; }

	public required string EarthquakeId { get; init; }
	public bool IsTrainingOrTest { get; init; }
	public bool IsVolcano { get; init; }
	public string? VolcanoName { get; init; }
	public DateTime? DetectedAt { get; init; }

	public JmaIntensity MaxIntensity { get; init; }
	public string MaxIntensityLongName => MaxIntensity.ToLongString();
	public JmaIntensity? PreviousMaxIntensity { get; init; }
	public LpgmIntensity? MaxLpgmIntensity { get; init; }
	public LpgmIntensity? PreviousMaxLpgmIntensity { get; init; }

	public bool IsCancelled { get; init; }

	public bool IsHypocenterOnly { get; init; }
	public bool IsDetailIntensityApplied { get; init; }

	public EarthquakeInformationEventHypocenter? Hypocenter { get; init; }

	/// <summary>
	/// 観測地域の差分情報
	/// </summary>
	public ObservationDiff? RegionDiff { get; init; }

	/// <summary>
	/// 地域の更新があったか（追加・削除・震度変化のいずれか）
	/// </summary>
	public bool IsRegionUpdated { get; init; }

	public string? Comment { get; init; }
	public string? FreeFormComment { get; init; }
}

public record EarthquakeInformationEventHypocenter(
	DateTime OccurrenceAt,
	string? PlaceName,
	Location? Location,
	float Magnitude,
	string? MagnitudeAlternativeText,
	int Depth,
	bool IsNoDepthData,
	bool IsVeryShallow,
	bool IsForeign
);
