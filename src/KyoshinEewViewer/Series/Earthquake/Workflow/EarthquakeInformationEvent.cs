using KyoshinEewViewer.Core;
using KyoshinEewViewer.Services.Workflows;
using KyoshinMonitorLib;
using System;

namespace KyoshinEewViewer.Series.Earthquake.Workflow;

public class EarthquakeInformationEvent() : WorkflowEvent("EarthquakeInformation")
{
	public DateTime UpdatedAt { get; init; }
	public required string LatestInformationName { get; init; }

	public required string EarthquakeId { get; init; }
	public bool IsTrainingOrTest { get; init; }
	public DateTime? DetectedAt { get; init; }

	public JmaIntensity MaxIntensity { get; init; }
	public JmaIntensity? PreviousMaxIntensity { get; init; }
	public LpgmIntensity? MaxLpgmIntensity { get; init; }
	public EarthquakeInformationEventHypocenter? Hypocenter { get; init; }

	// TODO: 実装したいがけっこう構造弄らないといけないかも
	// public List<ObservationIntensityGroup> Intensities { get; init; } = [];

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
	bool IsForeign
);

