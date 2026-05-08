using Avalonia;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Data;
using KyoshinEewViewer.Map.Layers;
using KyoshinEewViewer.Series.Earthquake.Models;
using KyoshinMonitorLib;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using static KyoshinEewViewer.Core.Models.KyoshinEewViewerConfiguration;

namespace KyoshinEewViewer.Series.Earthquake;

/// <summary>
/// Snapshot から地図描画用の派生データを組み立てる純関数的ヘルパー
/// </summary>
internal static class EarthquakeMapPresentationBuilder
{
	public static EarthquakeMapPresentation Build(
		EarthquakeSnapshot? snapshot,
		MapData? mapData,
		EarthquakeConfig config)
	{
		var areaItems = new Dictionary<JmaIntensity, List<(Location Location, string Name)>>();
		var cityItems = new Dictionary<JmaIntensity, List<(Location Location, string Name)>>();
		var stationItems = new Dictionary<JmaIntensity, List<(Location Location, string Name)>>();
		var colorMap = new Dictionary<LandLayerType, Dictionary<int, SKColor>>();
		var zoomPoints = new List<Location>();
		var hypocenters = new List<(Location Location, Location? Error)>();

		IntensityViewGroup[] groups = [];
		LpgmIntensityViewGroup[] lpgmGroups = [];
		Dictionary<LandLayerType, Dictionary<int, SKColor>>? lpgmColorMap = null;

		if (snapshot != null)
		{
			FeatureLayer? cityLayer = null;
			FeatureLayer? areaLayer = null;
			mapData?.TryGetLayer(LandLayerType.MunicipalityEarthquakeTsunamiArea, out cityLayer);
			mapData?.TryGetLayer(LandLayerType.EarthquakeInformationSubdivisionArea, out areaLayer);

			var mapSub = new Dictionary<int, SKColor>();
			var mapMun = new Dictionary<int, SKColor>();
			var onlyAreas = snapshot.Scope == EarthquakeIntensityScope.AreaOnly;

			foreach (var pref in snapshot.Prefectures)
			{
				foreach (var area in pref.Areas)
				{
					var areaIntensity = area.MaxIntensity;

					foreach (var city in area.Cities)
					{
						var cityIntensity = city.MaxIntensity;

						foreach (var station in city.Stations)
						{
							if (station.Location is not { } stationLoc)
								continue;

							if (!stationItems.TryGetValue(station.Intensity, out var stations))
								stationItems[station.Intensity] = stations = [];
							stations.Add((stationLoc, station.Name));

							zoomPoints.Add(new Location(stationLoc.Latitude - .1f, stationLoc.Longitude - .1f));
							zoomPoints.Add(new Location(stationLoc.Latitude + .1f, stationLoc.Longitude + .1f));
						}

						if (config.FillDetail)
							mapMun[city.Code] = FixedObjectRenderer.IntensityPaintCache[cityIntensity].Background.Color;

						if (city.CenterLocation is { } cityLoc)
						{
							if (!cityItems.TryGetValue(cityIntensity, out var cities))
								cityItems[cityIntensity] = cities = [];
							cities.Add((cityLoc, city.Name));

							if (cityLayer == null)
							{
								zoomPoints.Add(new Location(cityLoc.Latitude - .1f, cityLoc.Longitude - .1f));
								zoomPoints.Add(new Location(cityLoc.Latitude + .1f, cityLoc.Longitude + .1f));
							}
							else
							{
								foreach (var cityPoly in cityLayer.FindPolygon(city.Code))
								{
									zoomPoints.Add(cityPoly.BoundingBox.TopLeft.CastLocation());
									zoomPoints.Add(cityPoly.BoundingBox.BottomRight.CastLocation());
								}
							}
						}
					}

					if (area.CenterLocation is { } areaLoc)
					{
						if (!areaItems.TryGetValue(areaIntensity, out var areas))
							areaItems[areaIntensity] = areas = [];
						areas.Add((areaLoc, area.Name));
					}

					if (onlyAreas)
					{
						if (areaLayer == null && area.CenterLocation is { } al)
						{
							zoomPoints.Add(new Location(al.Latitude - .1f, al.Longitude - 1f));
							zoomPoints.Add(new Location(al.Latitude + .1f, al.Longitude + 1f));
						}
						if (areaLayer != null)
						{
							foreach (var p in areaLayer.FindPolygon(area.Code))
							{
								zoomPoints.Add(p.BoundingBox.TopLeft.CastLocation());
								zoomPoints.Add(p.BoundingBox.BottomRight.CastLocation());
							}
						}
						if (config.FillSokuhou)
							mapSub[area.Code] = FixedObjectRenderer.IntensityPaintCache[areaIntensity].Background.Color;
					}
				}
			}

			colorMap[LandLayerType.EarthquakeInformationSubdivisionArea] = mapSub;
			colorMap[LandLayerType.MunicipalityEarthquakeTsunamiArea] = mapMun;

			groups = BuildIntensityGroups(snapshot);

			if (snapshot.Scope == EarthquakeIntensityScope.Lpgm)
			{
				lpgmGroups = BuildLpgmIntensityGroups(snapshot);
				lpgmColorMap = BuildLpgmColorMap(snapshot, config);
			}
		}

		if (snapshot?.Hypocenter is { Location: { } hypocenterLocation } hypo)
		{
			hypocenters.Add((hypocenterLocation, hypo.LocationError));

			SortItems(hypocenterLocation, areaItems);
			SortItems(hypocenterLocation, cityItems);
			SortItems(hypocenterLocation, stationItems);

			var size = .1f;
			if (hypo.Magnitude >= 4)
				size = .3f;
			if ((hypo.Magnitude >= 6 && hypo.IsForeign) || hypo.IsVolcano)
				size = 30;

			zoomPoints.Add(new Location(hypocenterLocation.Latitude - size, hypocenterLocation.Longitude - size));
			zoomPoints.Add(new Location(hypocenterLocation.Latitude + size, hypocenterLocation.Longitude + size));
		}

		Rect? autoZoom = zoomPoints.Count > 0 ? zoomPoints.CalcRect() : null;

		return new EarthquakeMapPresentation(
			Hypocenters: hypocenters,
			AreaItems: areaItems,
			CityItems: cityItems.Count > 0 ? cityItems : null,
			StationItems: stationItems.Count > 0 ? stationItems : null,
			ColorMap: colorMap,
			AutoZoom: autoZoom,
			Groups: groups,
			LpgmGroups: lpgmGroups,
			LpgmColorMap: lpgmColorMap);
	}

	private static IntensityViewGroup[] BuildIntensityGroups(EarthquakeSnapshot snapshot)
	{
		var groups = new Dictionary<JmaIntensity, List<PrefectureIntensitySnapshot>>();

		foreach (var pref in snapshot.Prefectures)
		{
			var areasByIntensity = new Dictionary<JmaIntensity, List<AreaIntensitySnapshot>>();
			foreach (var area in pref.Areas)
			{
				if (snapshot.Scope == EarthquakeIntensityScope.AreaOnly)
				{
					Add(areasByIntensity, area.MaxIntensity, area with { Cities = [] });
				}
				else
				{
					var citiesByIntensity = new Dictionary<JmaIntensity, List<CityIntensitySnapshot>>();
					foreach (var city in area.Cities)
					{
						foreach (var stationGroup in city.Stations.GroupBy(s => s.Intensity))
						{
							Add(citiesByIntensity, stationGroup.Key,
								city with { Stations = stationGroup.ToList() });
						}
					}
					foreach (var (cityIntensity, cities) in citiesByIntensity)
					{
						Add(areasByIntensity, cityIntensity,
							area with { Cities = cities });
					}
				}
			}
			foreach (var (intensity, areas) in areasByIntensity)
			{
				Add(groups, intensity, pref with { Areas = areas });
			}
		}

		return groups
			.OrderByDescending(kv => SortKey(kv.Key))
			.Select(kv => new IntensityViewGroup(kv.Key, kv.Value))
			.ToArray();
	}

	private static LpgmIntensityViewGroup[] BuildLpgmIntensityGroups(EarthquakeSnapshot snapshot)
	{
		var groups = new Dictionary<LpgmIntensity, List<PrefectureIntensitySnapshot>>();

		foreach (var pref in snapshot.Prefectures)
		{
			var areasByIntensity = new Dictionary<LpgmIntensity, List<AreaIntensitySnapshot>>();
			foreach (var area in pref.Areas)
			{
				var citiesByIntensity = new Dictionary<LpgmIntensity, List<CityIntensitySnapshot>>();
				foreach (var city in area.Cities)
				{
					foreach (var stationGroup in city.Stations
						.Where(s => s.MaxLpgmIntensity is not null)
						.GroupBy(s => s.MaxLpgmIntensity!.Value))
					{
						Add(citiesByIntensity, stationGroup.Key,
							city with { Stations = stationGroup.ToList() });
					}
				}
				foreach (var (cityIntensity, cities) in citiesByIntensity)
				{
					Add(areasByIntensity, cityIntensity,
						area with { Cities = cities });
				}
			}
			foreach (var (intensity, areas) in areasByIntensity)
			{
				Add(groups, intensity, pref with { Areas = areas });
			}
		}

		return groups
			.OrderByDescending(kv => (int)kv.Key)
			.Select(kv => new LpgmIntensityViewGroup(kv.Key, kv.Value))
			.ToArray();
	}

	private static Dictionary<LandLayerType, Dictionary<int, SKColor>> BuildLpgmColorMap(EarthquakeSnapshot snapshot, EarthquakeConfig config)
	{
		var mapMun = new Dictionary<int, SKColor>();
		var mapSub = new Dictionary<int, SKColor>();

		if (config.FillDetail)
		{
			foreach (var pref in snapshot.Prefectures)
				foreach (var area in pref.Areas)
				{
					if (area.MaxLpgmIntensity is { } areaLpgm
						&& FixedObjectRenderer.LpgmIntensityPaintCache.TryGetValue(areaLpgm, out var areaPaints))
						mapSub[area.Code] = areaPaints.Background.Color;

					foreach (var city in area.Cities)
					{
						if (city.MaxLpgmIntensity is { } cityLpgm
							&& FixedObjectRenderer.LpgmIntensityPaintCache.TryGetValue(cityLpgm, out var cityPaints))
							mapMun[city.Code] = cityPaints.Background.Color;
					}
				}
		}

		return new Dictionary<LandLayerType, Dictionary<int, SKColor>>
		{
			[LandLayerType.EarthquakeInformationSubdivisionArea] = mapSub,
			[LandLayerType.MunicipalityEarthquakeTsunamiArea] = mapMun,
		};
	}

	private static int SortKey(JmaIntensity intensity)
		=> intensity switch
		{
			JmaIntensity.Unknown => ((int)JmaIntensity.Int5Lower) * 10 - 1,
			_ => ((int)intensity) * 10,
		};

	private static void Add<TKey, TValue>(Dictionary<TKey, List<TValue>> dict, TKey key, TValue value)
		where TKey : notnull
	{
		if (!dict.TryGetValue(key, out var list))
			dict[key] = list = [];
		list.Add(value);
	}

	private static void SortItems(Location hypocenter, Dictionary<JmaIntensity, List<(Location Location, string Name)>> items)
	{
		foreach (var item in items)
			item.Value.Sort((a, b)
				=> (int)(Math.Sqrt(Math.Pow(b.Location.Latitude - hypocenter.Latitude, 2) + Math.Pow(b.Location.Longitude - hypocenter.Longitude, 2)) * 1000) -
				   (int)(Math.Sqrt(Math.Pow(a.Location.Latitude - hypocenter.Latitude, 2) + Math.Pow(a.Location.Longitude - hypocenter.Longitude, 2)) * 1000));
	}
}

internal sealed record EarthquakeMapPresentation(
	List<(Location Location, Location? Error)> Hypocenters,
	Dictionary<JmaIntensity, List<(Location Location, string Name)>> AreaItems,
	Dictionary<JmaIntensity, List<(Location Location, string Name)>>? CityItems,
	Dictionary<JmaIntensity, List<(Location Location, string Name)>>? StationItems,
	Dictionary<LandLayerType, Dictionary<int, SKColor>> ColorMap,
	Rect? AutoZoom,
	IntensityViewGroup[] Groups,
	LpgmIntensityViewGroup[] LpgmGroups,
	Dictionary<LandLayerType, Dictionary<int, SKColor>>? LpgmColorMap);
