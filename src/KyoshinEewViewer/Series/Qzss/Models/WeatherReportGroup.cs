using Avalonia.Controls;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Jma;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Data;
using ReactiveUI;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Series.Qzss.Models;

public class WeatherReportGroup : DCReportGroup
{
	public static readonly string TYPE = "Weather";
	public override string Type => TYPE;

	private MapData? MapData { get; }
	private List<WeatherReport> Reports { get; } = [];

	private DateTime _reportTime;
	public DateTime ReportTime
	{
		get => _reportTime;
		set => this.RaiseAndSetIfChanged(ref _reportTime, value);
	}

	private int _totalAreaCount;
	public int TotalAreaCount
	{
		get => _totalAreaCount;
		set => this.RaiseAndSetIfChanged(ref _totalAreaCount, value);
	}

	public record WeatherWarning(byte SubCategory, bool IsCleared);
	public record WeatherWarningArea(int Region, List<WeatherWarning> Warnings);
	public ObservableCollection<WeatherWarningArea> WarningAreas { get; } = [];

	public WeatherReportGroup(WeatherReport report, MapData? mapData)
	{
		MapData = mapData;

		Classification = report.ReportClassification;
		InformationType = report.InformationType;

		ReportTime = report.ReportTime.LocalDateTime;

		Reports.Add(report);
		UpdateArea();
	}

	public override bool CheckDuplicate(DCReport report) => report is WeatherReport w && Reports.Any(r => w.Content.SequenceEqual(r.Content));
	public override bool TryProcess(DCReport report)
	{
		if (report is not WeatherReport w || w.ReportTime.LocalDateTime != ReportTime)
			return false;

		Reports.Add(w);
		ReportCount++;

		UpdateArea();
		return true;
	}

	[JsonIgnore]
	public override Control? DetailDisplayControl => new WeatherReportControl { DataContext = this };

	private void UpdateArea()
	{
		WarningAreas.Clear();
		foreach (var report in Reports)
		{
			var isCleared = report.WarningState == 2;
			foreach (var (subCategory, region) in report.Regions)
			{
				if (region == 0)
					continue;

				if (WarningAreas.FirstOrDefault(a => a.Region == region) is not { } area)
					WarningAreas.Add(area = new(region, []));

				area.Warnings.Add(new(subCategory, isCleared));
			}
		}

		var zoomPoints = new List<KyoshinMonitorLib.Location>();
		FeatureLayer? cityLayer = null;
		MapData?.TryGetLayer(LandLayerType.PrefectureForecastArea, out cityLayer);
		var size = new PointD(.1, .1);

		var map = new Dictionary<int, SKColor>();
		foreach (var area in WarningAreas)
		{
			var color = area.Warnings.Any(w => w.IsCleared) ? SKColors.Gray : SKColors.MediumOrchid;
			map[area.Region] = color;

			if (cityLayer != null)
			{
				foreach (var cityPoly in cityLayer.FindPolygon(area.Region))
				{
					zoomPoints.Add((cityPoly.BoundingBox.TopLeft - size).CastLocation());
					zoomPoints.Add((cityPoly.BoundingBox.BottomRight + size).CastLocation());
				}
			}
		}

		TotalAreaCount = WarningAreas.Count;

		MapDisplayParameter = new (){
			Padding = new(285, 0, 0, 0),
			CustomColorMap = new() {
				{ LandLayerType.PrefectureForecastArea, map },
			},
			LayerSets = [new(0, LandLayerType.PrefectureForecastArea)],
		};
		if (zoomPoints.Count != 0)
			MapNavigationRequest = new(zoomPoints.CalcRect());
		else
			MapNavigationRequest = null;
	}
}
