using KyoshinEewViewer.Series.Earthquake.Models;

namespace KyoshinEewViewer.Series.Earthquake.Services;

/// <summary>
/// 観測地域の差分を計算するインターフェイス
/// </summary>
public interface IObservationDiffCalculator
{
	/// <summary>
	/// 階層構造の観測情報から差分を計算する
	/// </summary>
	ObservationDiff ComputeFromPrefs(
		EarthquakeObservationPref[]? previous,
		EarthquakeObservationPref[]? current);

	/// <summary>
	/// フラット構造の観測情報から差分を計算する（P2P地震情報用）
	/// </summary>
	ObservationDiff ComputeFromFlatPoints(
		EarthquakeObservationFlatPoint[]? previous,
		EarthquakeObservationFlatPoint[]? current);
}
