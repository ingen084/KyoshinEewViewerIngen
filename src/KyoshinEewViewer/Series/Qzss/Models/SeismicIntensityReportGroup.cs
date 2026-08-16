using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Jma;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Data;
using KyoshinMonitorLib;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Series.Qzss.Models;

public class SeismicIntensityReportGroup : DCReportGroup
{
	public static readonly string TYPE = "SeismicIntensity";
	public override string Type => TYPE;

	private List<SeismicIntensityReport> Reports { get; } = [];

	private DateTime _occurrenceTime;
	public DateTime OccurrenceTime
	{
		get => _occurrenceTime;
		set => SetProperty(ref _occurrenceTime, value);
	}

	private int _totalAreaCount;
	public int TotalAreaCount
	{
		get => _totalAreaCount;
		set => SetProperty(ref _totalAreaCount, value);
	}

	private SeismicIntensity _maxIntensity;
	public SeismicIntensity MaxIntensity
	{
		get => _maxIntensity;
		set => SetProperty(ref _maxIntensity, value);
	}

	public record SeismicIntensityRegionGroup(JmaIntensity Intensity, List<string> RegionNames)
	{
		public bool IsLessThanInt4 => Intensity == JmaIntensity.Int3;
	}
	public ObservableCollection<SeismicIntensityRegionGroup> RegionGroups { get; } = [];

	private MapData? MapData { get; set; }

	public SeismicIntensityReportGroup(SeismicIntensityReport report, MapData? mapData)
	{
		MapData = mapData;

		Classification = report.ReportClassification;
		InformationType = report.InformationType;

		ReportTime = ApplyTimezoneOffset(report.ReportTime);
		OccurrenceTime = ApplyTimezoneOffset(report.OccurrenceTime);
		MaxIntensity = report.Regions.Max(r => r.Intensity);

		Reports.Add(report);

		UpdateMap();
	}

	public override bool CheckDuplicate(DCReport report) => report is SeismicIntensityReport si && Reports.Any(r => si.Content.SequenceEqual(r.Content));
	public override bool TryProcess(DCReport report)
	{
		if (report is not SeismicIntensityReport si || ApplyTimezoneOffset(si.ReportTime) != ReportTime)
			return false;

		Reports.Add(si);
		ReportCount++;
		var max = si.Regions.Max(r => r.Intensity);
		if (max > MaxIntensity)
			MaxIntensity = max;

		UpdateMap();
		return true;
	}

	private void UpdateMap()
	{
		var zoomPoints = new List<KyoshinMonitorLib.Location>();

		FeatureLayer? cityLayer = null;
		MapData?.TryGetLayer(LandLayerType.EarthquakeInformationPrefecture, out cityLayer);

		var map = new Dictionary<int, SKColor>();
		var size = new PointD(.1, .1);

		var areaDictionary = new Dictionary<string, JmaIntensity>();

		foreach (var r in Reports)
		{
			foreach (var (i, c) in r.Regions)
			{
				if (c == 0)
					continue;

				var areaIntensity = i switch
				{
					SeismicIntensity.LessThanInt4 => JmaIntensity.Int3,
					SeismicIntensity.Int4 => JmaIntensity.Int4,
					SeismicIntensity.Int5Lower => JmaIntensity.Int5Lower,
					SeismicIntensity.Int5Upper => JmaIntensity.Int5Upper,
					SeismicIntensity.Int6Lower => JmaIntensity.Int6Lower,
					SeismicIntensity.Int6Upper => JmaIntensity.Int6Upper,
					SeismicIntensity.Int7 => JmaIntensity.Int7,
					_ => JmaIntensity.Unknown,
				};
				map[c] = FixedObjectRenderer.IntensityPaintCache[areaIntensity].Background.Color;

				// 同じエリアが存在する場合、震度が高い方を優先
				var areaName = CsvDictionary.AreaInformationPrefectureEarthquake.TryGetValue(c, out var s) ? s : $"不明({c})";
				if (!areaDictionary.TryGetValue(areaName, out var ci) || areaIntensity > ci)
					areaDictionary[areaName] = areaIntensity;

				if (cityLayer != null)
				{
					foreach (var cityPoly in cityLayer.FindPolygon(c))
					{
						zoomPoints.Add((cityPoly.BoundingBox.TopLeft - size).CastLocation());
						zoomPoints.Add((cityPoly.BoundingBox.BottomRight + size).CastLocation());
					}
				}
			}
		}

		TotalAreaCount = areaDictionary.Count;
		RegionGroups.Clear();
		foreach (var (name, intensity) in areaDictionary.OrderByDescending(a => a.Value))
		{
			if (RegionGroups.Any(g => g.Intensity == intensity))
				RegionGroups.First(g => g.Intensity == intensity).RegionNames.Add(name);
			else
				RegionGroups.Add(new(intensity, [name]));
		}

		MapDisplayParameter = MapDisplayParameter with
		{
			Padding = new(245, 0, 0, 0),
			CustomColorMap = new() {
				{ LandLayerType.EarthquakeInformationPrefecture, map },
			},
			LayerSets = [new(0, LandLayerType.EarthquakeInformationPrefecture)],
		};

		if (zoomPoints.Count != 0)
			MapNavigationRequest = new(zoomPoints.CalcRect());
		else
			MapNavigationRequest = null;
	}

	[JsonIgnore]
	public override Control? DetailDisplayControl => new SeismicIntensityReportControl { DataContext = this };
}
