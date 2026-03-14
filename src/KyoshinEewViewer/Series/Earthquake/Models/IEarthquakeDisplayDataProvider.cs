using KyoshinEewViewer.JmaXmlParser;
using KyoshinEewViewer.Series.Earthquake.Converters;
using KyoshinEewViewer.Services.TelegramPublishers;
using KyoshinMonitorLib;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.Earthquake.Models;

/// <summary>
/// 表示用の震度観測データ（遅延パースの結果）
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
	/// フラット構造の観測情報 (P2P用)
	/// </summary>
	public EarthquakeObservationFlatPoint[]? FlatPoints { get; init; }
}

/// <summary>
/// Fragmentに紐づく表示データプロバイダ
/// 電文受信時ではなく、表示時に遅延パースして観測データを提供する
/// </summary>
public interface IEarthquakeDisplayDataProvider
{
	/// <summary>
	/// 震度観測データを遅延パースして取得する
	/// </summary>
	Task<EarthquakeDisplayIntensityData?> GetIntensityDataAsync();

	/// <summary>
	/// 地図表示が可能か (P2Pソースではfalse)
	/// </summary>
	bool SupportsMapDisplay { get; }
}

/// <summary>
/// JMA XML電文を遅延パースして表示データを提供するプロバイダ
/// Telegramへの参照を保持し、GetIntensityDataAsync()呼び出し時にXMLを再パースする
/// </summary>
internal class JmaXmlDisplayDataProvider(Telegram telegram, bool onlyAreas) : IEarthquakeDisplayDataProvider
{
	public bool SupportsMapDisplay => true;

	public async Task<EarthquakeDisplayIntensityData?> GetIntensityDataAsync()
	{
		await using var stream = await telegram.GetBodyAsync();
		using var report = new JmaXmlDocument(stream);

		if (report.EarthquakeBody.Intensity?.Observation is not { } observation)
			return null;

		var prefs = JmaXmlEarthquakeConverter.BuildObservationPrefs(observation, onlyAreas);
		return new EarthquakeDisplayIntensityData
		{
			MaxIntensity = observation.MaxInt?.ToJmaIntensity() ?? JmaIntensity.Unknown,
			ObservationPrefs = prefs,
		};
	}
}
