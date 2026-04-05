using KyoshinMonitorLib;

namespace KyoshinEewViewer.Series.Earthquake.Models;

/// <summary>
/// 表示用の震度観測データ
/// </summary>
public class EarthquakeDisplayIntensityData
{
	/// <summary>
	/// 最大震度
	/// </summary>
	public required JmaIntensity MaxIntensity { get; init; }

	/// <summary>
	/// 観測情報の階層構造 (JMA XML / AXIS用)
	/// </summary>
	public EarthquakeObservationPref[]? ObservationPrefs { get; init; }

	/// <summary>
	/// フラット構造の観測情報 (P2P地震情報用)
	/// </summary>
	public EarthquakeObservationFlatPoint[]? FlatPoints { get; init; }
}
