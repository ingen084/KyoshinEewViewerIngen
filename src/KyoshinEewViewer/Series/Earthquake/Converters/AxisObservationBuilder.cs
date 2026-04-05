using KyoshinEewViewer.Series.Earthquake.Models;
using KyoshinEewViewer.Services.ExtarnalPublishers.Axis.ApiModels.Message;
using KyoshinMonitorLib;
using System.Collections.Generic;

namespace KyoshinEewViewer.Series.Earthquake.Converters;

/// <summary>
/// AXIS JSONの観測データ構築
/// </summary>
internal static class AxisObservationBuilder
{
	/// <summary>
	/// AXIS の Pref[] > Area[] > City[] > IntensityStation[] 階層を EarthquakeObservationPref[] に変換する
	/// </summary>
	internal static EarthquakeObservationPref[] BuildObservationPrefs(Observation observation, bool onlyAreas)
	{
		if (observation.Pref == null)
			return [];

		var prefs = new List<EarthquakeObservationPref>();

		foreach (var pref in observation.Pref)
		{
			if (pref.Area == null)
				continue;

			var areas = new List<EarthquakeObservationArea>();

			foreach (var area in pref.Area)
			{
				var areaIntensity = area.MaxInt?.ToJmaIntensity() ?? JmaIntensity.Unknown;

				if (onlyAreas)
				{
					areas.Add(new EarthquakeObservationArea
					{
						Name = area.Name ?? "不明",
						Code = int.TryParse(area.Code, out var ac) ? ac : 0,
						MaxIntensity = areaIntensity,
					});
					continue;
				}

				var cities = new List<EarthquakeObservationCity>();

				if (area.City != null)
				{
					foreach (var city in area.City)
					{
						var cityIntensity = city.MaxInt?.ToJmaIntensity() ?? JmaIntensity.Unknown;

						var stations = new List<EarthquakeObservationStation>();
						if (city.IntensityStation != null)
						{
							foreach (var station in city.IntensityStation)
							{
								stations.Add(new EarthquakeObservationStation
								{
									Name = station.Name ?? "不明",
									Code = station.Code ?? "",
									Intensity = station.Int?.ToJmaIntensity() ?? JmaIntensity.Unknown,
								});
							}
						}

						cities.Add(new EarthquakeObservationCity
						{
							Name = city.Name ?? "不明",
							Code = int.TryParse(city.Code, out var cc) ? cc : 0,
							MaxIntensity = cityIntensity,
							Stations = stations.ToArray(),
						});
					}
				}

				areas.Add(new EarthquakeObservationArea
				{
					Name = area.Name ?? "不明",
					Code = int.TryParse(area.Code, out var areaCode) ? areaCode : 0,
					MaxIntensity = areaIntensity,
					Cities = cities.ToArray(),
				});
			}

			prefs.Add(new EarthquakeObservationPref
			{
				Name = pref.Name ?? "不明",
				Code = int.TryParse(pref.Code, out var pc) ? pc : 0,
				Areas = areas.ToArray(),
			});
		}

		return prefs.ToArray();
	}
}
