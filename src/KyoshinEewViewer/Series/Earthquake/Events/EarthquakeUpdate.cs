using KyoshinEewViewer.Series.Earthquake.Models;
using KyoshinMonitorLib;

namespace KyoshinEewViewer.Series.Earthquake.Events;

/// <summary>
/// EarthquakeWatchService から購読者へ通知される地震情報更新イベント
/// </summary>
public record EarthquakeUpdate(
	EarthquakeEvent Earthquake,
	bool IsBulkInserting,
	bool IsDryRun,
	EarthquakeInformationFragment? Fragment,
	JmaIntensity? PreviousMaxIntensity);
