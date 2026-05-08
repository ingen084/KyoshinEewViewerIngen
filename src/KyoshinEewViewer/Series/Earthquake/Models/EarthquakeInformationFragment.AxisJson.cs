using DmdataSharp.ApiResponses.V2.Parameters;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Series.Earthquake.Services;
using KyoshinEewViewer.Services.ExtarnalPublishers.Axis.ApiModels.Message;
using KyoshinMonitorLib;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace KyoshinEewViewer.Series.Earthquake.Models;

public abstract partial class EarthquakeInformationFragment
{
	public static EarthquakeInformationFragment? CreateFromAxisJson(
		EarthquakeMessage message,
		IReadOnlyList<EarthquakeStationParameterResponse.Item>? stations = null)
	{
		if (message.Control == null || message.Head == null)
			return null;

		switch (message.Control.Title)
		{
			case "震源に関する情報":
			case "顕著な地震の震源要素更新のお知らせ":
				return BuildHypocenterFragmentFromAxis(message);
			case "震度速報":
				return BuildIntensityFragmentFromAxis(message, stations);
			case "震源・震度に関する情報":
				return BuildHypocenterAndIntensityFragmentFromAxis(message, stations);
			case "長周期地震動に関する観測情報":
				return BuildLpgmFragmentFromAxis(message, stations);
			default:
				return null;
		}
	}

	private static HypocenterInformationFragment? BuildHypocenterFragmentFromAxis(EarthquakeMessage msg)
	{
		var earthquake = msg.Body?.Earthquake?.FirstOrDefault();
		if (earthquake == null) return null;
		if (earthquake.OriginTime is not { } occurrenceTime) return null;

		var (location, locationError, depth, depthError) = ParseHypocenterCoordinatesFromAxis(earthquake.Hypocenter.Area);
		if (location == null) return null;
		if (!TryParseMagnitude(earthquake.Magnitude, out var magnitude, out var magnitudeAlt))
			return null;

		return new HypocenterInformationFragment
		{
			ArrivedTime = msg.Head.ReportDateTime,
			Title = msg.Control.Title,
			IsTest = msg.Control.Status == "試験",
			IsTraining = msg.Control.Status == "訓練",

			OccurrenceTime = occurrenceTime,
			Place = earthquake.Hypocenter.Area.Name,
			Magnitude = magnitude,
			MagnitudeAlternativeText = magnitudeAlt,
			Depth = depth,
			DepthError = depthError,
			Location = location,
			LocationError = locationError,

			Comment = msg.Body?.Comments?.ForecastComment?.Text,
			FreeFormComment = msg.Body?.Comments?.FreeFormComment?.Text,
		};
	}

	private static IntensityInformationFragment? BuildIntensityFragmentFromAxis(
		EarthquakeMessage msg,
		IReadOnlyList<EarthquakeStationParameterResponse.Item>? stations)
	{
		var observation = msg.Body?.Intensity?.Observation;
		if (observation == null) return null;
		if (observation.MaxInt?.ToJmaIntensity() is not { } maxIntensity) return null;
		if (msg.Head.TargetDateTime is not { } detectionTime) return null;

		string? areaName = null;
		var isSingleArea = true;
		if (observation.Pref != null)
		{
			foreach (var pref in observation.Pref)
			{
				if (!isSingleArea) break;
				if (pref.Area == null) continue;
				foreach (var area in pref.Area)
				{
					if (areaName != null && isSingleArea)
					{
						isSingleArea = false;
						break;
					}
					areaName = area.Name;
				}
			}
		}
		if (areaName == null) return null;

		return new IntensityInformationFragment
		{
			ArrivedTime = msg.Head.ReportDateTime,
			Title = msg.Control.Title,
			IsTest = msg.Control.Status == "試験",
			IsTraining = msg.Control.Status == "訓練",

			Place = areaName,
			DetectionTime = detectionTime,
			MaxIntensity = maxIntensity,
			IsSingleArea = isSingleArea,
			Comment = msg.Body?.Comments?.ForecastComment?.Text,
			FreeFormComment = msg.Body?.Comments?.FreeFormComment?.Text,
			Observation = BuildObservationTreeFromAxis(observation, EarthquakeIntensityScope.AreaOnly, stations),
		};
	}

	private static HypocenterAndIntensityInformationFragment? BuildHypocenterAndIntensityFragmentFromAxis(
		EarthquakeMessage msg,
		IReadOnlyList<EarthquakeStationParameterResponse.Item>? stations)
	{
		var earthquake = msg.Body?.Earthquake?.FirstOrDefault();
		if (earthquake == null) return null;
		if (earthquake.OriginTime is not { } occurrenceTime) return null;

		var (location, locationError, depth, depthError) = ParseHypocenterCoordinatesFromAxis(earthquake.Hypocenter.Area);
		if (location == null) return null;
		if (!TryParseMagnitude(earthquake.Magnitude, out var magnitude, out var magnitudeAlt))
			return null;

		MatchCollection? volcanoMatches = null;
		if (msg.Body?.Comments?.FreeFormComment?.Text is string fc)
			volcanoMatches = VolcanoMatchRegex().Matches(fc);

		return new HypocenterAndIntensityInformationFragment
		{
			ArrivedTime = msg.Head.ReportDateTime,
			Title = msg.Control.Title,
			IsTest = msg.Control.Status == "試験",
			IsTraining = msg.Control.Status == "訓練",

			OccurrenceTime = occurrenceTime,
			Place = earthquake.Hypocenter.Area.Name,
			Magnitude = magnitude,
			MagnitudeAlternativeText = magnitudeAlt,
			Depth = depth,
			DepthError = depthError,
			Location = location,
			LocationError = locationError,

			MaxIntensity = msg.Body?.Intensity?.Observation?.MaxInt?.ToJmaIntensity() ?? JmaIntensity.Unknown,
			IsForeign = msg.Head.Title == "遠地地震に関する情報",
			IsVolcano = (volcanoMatches?.Count ?? 0) > 0,
			VolcanoName = volcanoMatches?.FirstOrDefault()?.Groups[2].Value,

			Comment = msg.Body?.Comments?.ForecastComment?.Text,
			FreeFormComment = msg.Body?.Comments?.FreeFormComment?.Text,
			Observation = BuildObservationTreeFromAxis(msg.Body?.Intensity?.Observation, EarthquakeIntensityScope.Detail, stations),
		};
	}

	private static LpgmIntensityInformationFragment? BuildLpgmFragmentFromAxis(
		EarthquakeMessage msg,
		IReadOnlyList<EarthquakeStationParameterResponse.Item>? stations)
	{
		var earthquake = msg.Body?.Earthquake?.FirstOrDefault();
		if (earthquake == null) return null;
		if (earthquake.OriginTime is not { } occurrenceTime) return null;

		var (location, locationError, depth, depthError) = ParseHypocenterCoordinatesFromAxis(earthquake.Hypocenter.Area);
		if (location == null) return null;
		if (!TryParseMagnitude(earthquake.Magnitude, out var magnitude, out var magnitudeAlt))
			return null;

		return new LpgmIntensityInformationFragment
		{
			ArrivedTime = msg.Head.ReportDateTime,
			Title = msg.Control.Title,
			IsTest = msg.Control.Status == "試験",
			IsTraining = msg.Control.Status == "訓練",

			OccurrenceTime = occurrenceTime,
			Place = earthquake.Hypocenter.Area.Name,
			Magnitude = magnitude,
			MagnitudeAlternativeText = magnitudeAlt,
			Depth = depth,
			DepthError = depthError,
			Location = location,
			LocationError = locationError,

			MaxIntensity = msg.Body?.Intensity?.Observation?.MaxInt?.ToJmaIntensity() ?? JmaIntensity.Unknown,
			MaxLpgmIntensity = msg.Body?.Intensity?.Observation?.MaxLgInt?.ToLpgmIntensity() ?? LpgmIntensity.Unknown,
			IsForeign = false,
			IsVolcano = false,

			Comment = msg.Body?.Comments?.ForecastComment?.Text,
			FreeFormComment = msg.Body?.Comments?.FreeFormComment?.Text,
			Observation = BuildObservationTreeFromAxis(msg.Body?.Intensity?.Observation, EarthquakeIntensityScope.Lpgm, stations),
		};
	}

	private static (Location? Location, Location? LocationError, int Depth, int? DepthError) ParseHypocenterCoordinatesFromAxis(HypocenterArea area)
	{
		Location? location = null;
		Location? locationError = null;
		var depth = -1;
		int? depthError = null;

		Location? degreeMinuteLocation = null;
		Location? degreeMinuteLocationError = null;
		var degreeMinuteDepth = -1;
		int? degreeMinuteDepthError = null;
		var hasDegreeMinute = false;

		if (area.Coordinate != null)
		{
			foreach (var c in area.Coordinate)
			{
				var value = c.ValueOf;
				if (c.Type == "震源位置（度分）")
				{
					hasDegreeMinute = true;
					degreeMinuteLocation = CoordinateConverter.GetLocationFromDegreeMinute(value);
					degreeMinuteLocationError = new Location(0.5f / 60f, 0.5f / 60f);
					degreeMinuteDepth = CoordinateConverter.GetDepth(value) ?? degreeMinuteDepth;
					degreeMinuteDepthError = 1;
					continue;
				}
				location = CoordinateConverter.GetLocation(value);
				locationError = new Location(0.05f, 0.05f);
				depth = CoordinateConverter.GetDepth(value) ?? -1;
				depthError = 10;
			}
		}

		if (hasDegreeMinute)
		{
			if (degreeMinuteLocation != null)
			{
				location = degreeMinuteLocation;
				locationError = degreeMinuteLocationError;
			}
			if (degreeMinuteDepth >= 0)
			{
				depth = degreeMinuteDepth;
				depthError = degreeMinuteDepthError;
			}
		}
		return (location, locationError, depth, depthError);
	}

	private static bool TryParseMagnitude(PhysicalQuantity[]? magnitudes, out float magnitude, out string? alternativeText)
	{
		magnitude = float.NaN;
		alternativeText = null;
		var first = magnitudes?.FirstOrDefault();
		if (first == null)
			return false;
		if (float.TryParse(first.ValueOf, NumberStyles.Float, CultureInfo.InvariantCulture, out var m))
		{
			magnitude = m;
			alternativeText = float.IsNaN(m) ? first.Description : null;
			return true;
		}
		// マグニチュードが NaN（不明）扱い: description を返す
		magnitude = float.NaN;
		alternativeText = first.Description;
		return true;
	}

	private static IReadOnlyList<PrefectureIntensitySnapshot> BuildObservationTreeFromAxis(
		Observation? observation,
		EarthquakeIntensityScope scope,
		IReadOnlyList<EarthquakeStationParameterResponse.Item>? stations)
	{
		if (observation?.Pref == null)
			return [];

		var prefs = new List<PrefectureIntensitySnapshot>();
		foreach (var pref in observation.Pref)
		{
			var areas = new List<AreaIntensitySnapshot>();
			if (pref.Area != null)
			{
				foreach (var area in pref.Area)
				{
					var cities = new List<CityIntensitySnapshot>();
					if (area.City != null)
					{
						foreach (var city in area.City)
						{
							var stationsList = new List<StationIntensitySnapshot>();
							if (city.IntensityStation != null)
							{
								foreach (var st in city.IntensityStation)
								{
									if (st.Code == null || st.Name == null || st.Int == null) continue;
									var stInfo = stations?.FirstOrDefault(s => s.Code == st.Code);

									IReadOnlyList<LgIntByPeriod>? lpgmByPeriod = null;
									LpgmIntensity? maxLpgm = null;
									if (scope == EarthquakeIntensityScope.Lpgm && st.LgIntPerPeriod != null)
									{
										var pairs = new List<LgIntByPeriod>();
										foreach (var p in st.LgIntPerPeriod)
										{
											if (p.PeriodicBand == null || p.ValueOf == null) continue;
											if (!int.TryParse(p.PeriodicBand, NumberStyles.Integer, CultureInfo.InvariantCulture, out var band)) continue;
											pairs.Add(new LgIntByPeriod(band, p.ValueOf.ToLpgmIntensity()));
										}
										if (pairs.Count > 0)
										{
											lpgmByPeriod = pairs;
											maxLpgm = pairs.Max(p => p.Intensity);
										}
									}

									stationsList.Add(new StationIntensitySnapshot(
										Name: string.Intern(st.Name),
										Code: int.TryParse(st.Code, NumberStyles.None, CultureInfo.InvariantCulture, out var stCode) ? stCode : 0,
										Intensity: st.Int.ToJmaIntensity(),
										MaxLpgmIntensity: maxLpgm,
										LpgmByPeriod: lpgmByPeriod,
										Location: stInfo?.GetLocation()));
								}
							}
							if (city.Code == null || city.Name == null) continue;
							if (!int.TryParse(city.Code, NumberStyles.None, CultureInfo.InvariantCulture, out var cityCode)) continue;
							cities.Add(new CityIntensitySnapshot(
								Name: string.Intern(city.Name),
								Code: cityCode,
								MaxIntensity: city.MaxInt?.ToJmaIntensity() ?? JmaIntensity.Unknown,
								MaxLpgmIntensity: city.MaxLgInt?.ToLpgmIntensity(),
								CenterLocation: RegionCenterLocations.Default.GetLocation(LandLayerType.MunicipalityEarthquakeTsunamiArea, cityCode),
								Stations: stationsList));
						}
					}
					if (area.Code == null || area.Name == null) continue;
					if (!int.TryParse(area.Code, NumberStyles.None, CultureInfo.InvariantCulture, out var areaCode)) continue;
					areas.Add(new AreaIntensitySnapshot(
						Name: string.Intern(area.Name),
						Code: areaCode,
						MaxIntensity: area.MaxInt?.ToJmaIntensity() ?? JmaIntensity.Unknown,
						MaxLpgmIntensity: area.MaxLgInt?.ToLpgmIntensity(),
						CenterLocation: RegionCenterLocations.Default.GetLocation(LandLayerType.EarthquakeInformationSubdivisionArea, areaCode),
						Cities: cities));
				}
			}
			if (pref.Code == null || pref.Name == null) continue;
			if (!int.TryParse(pref.Code, NumberStyles.None, CultureInfo.InvariantCulture, out var prefCode)) continue;
			prefs.Add(new PrefectureIntensitySnapshot(
				Name: string.Intern(pref.Name),
				Code: prefCode,
				MaxIntensity: pref.MaxInt?.ToJmaIntensity() ?? JmaIntensity.Unknown,
				MaxLpgmIntensity: pref.MaxLgInt?.ToLpgmIntensity(),
				Areas: areas));
		}
		return prefs;
	}
}
