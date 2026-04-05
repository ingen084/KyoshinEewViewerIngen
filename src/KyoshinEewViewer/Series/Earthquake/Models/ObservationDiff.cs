using KyoshinMonitorLib;

namespace KyoshinEewViewer.Series.Earthquake.Models;

/// <summary>
/// 観測地域の差分情報
/// </summary>
public class ObservationDiff
{
	/// <summary>
	/// 追加された地域
	/// </summary>
	public required ObservationAreaChange[] AddedAreas { get; init; }

	/// <summary>
	/// 削除された地域
	/// </summary>
	public required ObservationAreaChange[] RemovedAreas { get; init; }

	/// <summary>
	/// 震度が変化した地域
	/// </summary>
	public required ObservationAreaIntensityChange[] IntensityChangedAreas { get; init; }

	/// <summary>
	/// 何らかの変更があるか
	/// </summary>
	public bool HasChanges => AddedAreas.Length > 0 || RemovedAreas.Length > 0 || IntensityChangedAreas.Length > 0;

	/// <summary>
	/// 変更なしの差分
	/// </summary>
	public static ObservationDiff Empty { get; } = new()
	{
		AddedAreas = [],
		RemovedAreas = [],
		IntensityChangedAreas = [],
	};
}

/// <summary>
/// 地域の追加/削除情報
/// </summary>
public record ObservationAreaChange(int Code, string Name, JmaIntensity Intensity);

/// <summary>
/// 地域の震度変化情報
/// </summary>
public record ObservationAreaIntensityChange(int Code, string Name, JmaIntensity PreviousIntensity, JmaIntensity CurrentIntensity);
