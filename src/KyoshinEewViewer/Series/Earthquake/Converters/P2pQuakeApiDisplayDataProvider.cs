using KyoshinEewViewer.Series.Earthquake.Models;
using KyoshinEewViewer.Services.ExtarnalPublishers.P2pQuakeApi.ApiModels;
using KyoshinMonitorLib;
using System.Linq;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.Earthquake.Converters;

/// <summary>
/// P2P地震情報の観測点データを提供するプロバイダ
/// </summary>
internal class P2pQuakeApiDisplayDataProvider(P2pQuakeApiEarthquakePoint[] points, JmaIntensity maxIntensity) : IEarthquakeDisplayDataProvider
{
	public bool SupportsMapDisplay => false;

	public Task<EarthquakeDisplayIntensityData?> GetIntensityDataAsync()
	{
		var flatPoints = points
			.Select(p => new EarthquakeObservationFlatPoint
			{
				PrefName = p.Pref ?? "不明",
				Address = p.Addr ?? "不明",
				Intensity = P2pQuakeApiScaleConverter.ToJmaIntensity(p.Scale),
				IsArea = p.IsArea,
			})
			.ToArray();

		return Task.FromResult<EarthquakeDisplayIntensityData?>(new EarthquakeDisplayIntensityData
		{
			MaxIntensity = maxIntensity,
			FlatPoints = flatPoints,
		});
	}
}
