using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
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

public class AshFallReportGroup : DCReportGroup
{
	public static readonly string TYPE = "AshFall";
	public override string Type => TYPE;

	private List<AshFallReport> Reports { get; } = [];

	private DateTime _reportTime;
	public DateTime ReportTime
	{
		get => _reportTime;
		set => this.RaiseAndSetIfChanged(ref _reportTime, value);
	}

	private DateTime _activityTime;
	public DateTime ActivityTime
	{
		get => _activityTime;
		set => this.RaiseAndSetIfChanged(ref _activityTime, value);
	}

	private int _totalAreaCount;
	public int TotalAreaCount
	{
		get => _totalAreaCount;
		set => this.RaiseAndSetIfChanged(ref _totalAreaCount, value);
	}

	private int _volcanoNameCode;
	public int VolcanoNameCode
	{
		get => _volcanoNameCode;
		set => this.RaiseAndSetIfChanged(ref _volcanoNameCode, value);
	}

	private byte _warningType;
	public byte WarningType
	{
		get => _warningType;
		set => this.RaiseAndSetIfChanged(ref _warningType, value);
	}

	private byte _mapCursor = 1;
	public byte MapCursor
	{
		get => _mapCursor;
		set => this.RaiseAndSetIfChanged(ref _mapCursor, value);
	}

	private byte _maxMapTime = 1;
	public byte MaxMapTime
	{
		get => _maxMapTime;
		set => this.RaiseAndSetIfChanged(ref _maxMapTime, value);
	}

	public record AshFallArea(int Region, List<byte> WarningCodes);
	public record AshFallTime(DateTime Time, byte ExpectedTime, List<AshFallArea> Areas);

	public ObservableCollection<AshFallTime> AshFallTimes { get; } = [];
	private Thickness FixedPadding { get; } = new(235, 0, 0, 0);

	private MapData? MapData { get; set; }

	public AshFallReportGroup(AshFallReport report, MapData? mapData)
	{
		MapData = mapData;

		Classification = report.ReportClassification;
		InformationType = report.InformationType;

		ReportTime = report.ReportTime.LocalDateTime;
		TotalAreaCount = report.Regions.Count(a => a.Region != 0);
		VolcanoNameCode = report.VolcanoNameCode;
		ActivityTime = report.ActivityTime.LocalDateTime;
		WarningType = report.WarningType;

		Reports.Add(report);
		UpdateDetails();

		MapDisplayParameter = new()
		{
			Padding = FixedPadding,
			LayerSets = [
				new(10, LandLayerType.MunicipalityWeatherWarningArea),
				new(0, LandLayerType.PrefectureForecastArea),
			],
		};

		this.WhenAnyValue(a => a.MapCursor).Subscribe(a => UpdateMapDisplay());
	}

	public override bool CheckDuplicate(DCReport report) => report is AshFallReport a && Reports.Any(r => a.Content.SequenceEqual(r.Content));
	public override bool TryProcess(DCReport report)
	{
		if (report is not AshFallReport a || a.ReportTime.LocalDateTime != ReportTime || a.VolcanoNameCode != VolcanoNameCode || a.WarningType != WarningType)
			return false;

		Reports.Add(a);
		ReportCount++;
		TotalAreaCount += a.Regions.Count(a => a.Region != 0);

		UpdateDetails();

		MapCursor = a.Regions.Max(a => a.ExpectedTime);
		return true;
	}

	[JsonIgnore]
	public override Control? DetailDisplayControl => new AshFallReportControl { DataContext = this };

	private void UpdateDetails()
	{
		AshFallTimes.Clear();

		var ashFallTimes = new List<AshFallTime>();
		foreach (var report in Reports)
		{
			foreach (var (expectedTime, warningCode, region) in report.Regions)
			{
				if (expectedTime == 0)
					continue;
				MaxMapTime = Math.Max(MaxMapTime, expectedTime);
				var ashFallTime = ashFallTimes.FirstOrDefault(a => a.ExpectedTime == expectedTime);
				if (ashFallTime == null)
				{
					ashFallTime = new(ActivityTime.AddHours(expectedTime), expectedTime, []);
					ashFallTimes.Add(ashFallTime);
				}
				var ashFallArea = ashFallTime.Areas.FirstOrDefault(a => a.Region == region);
				if (ashFallArea == null)
				{
					ashFallArea = new(region, []);
					ashFallTime.Areas.Add(ashFallArea);
					ashFallTime.Areas.Sort((a, b) => a.Region.CompareTo(b.Region));
				}
				if (!ashFallArea.WarningCodes.Contains(warningCode))
				{
					ashFallArea.WarningCodes.Add(warningCode);
					ashFallArea.WarningCodes.Sort();
				}
			}
		}

		foreach (var t in ashFallTimes.OrderBy(a => a.Time))
			AshFallTimes.Add(t);
		UpdateMapDisplay();
	}

	private void UpdateMapDisplay()
	{
		var t = AshFallTimes.FirstOrDefault(t => t.ExpectedTime == MapCursor);
		if (t == null)
		{
			MapDisplayParameter = MapDisplayParameter with
			{
				CustomColorMap = null,
			};
			return;
		}
		var zoomPoints = new List<KyoshinMonitorLib.Location>();
		var map = new Dictionary<int, SKColor>();
		var size = new PointD(.1, .1);

		FeatureLayer? cityLayer = null;
		MapData?.TryGetLayer(LandLayerType.MunicipalityWeatherWarningArea, out cityLayer);

		foreach (var area in t.Areas)
		{
			map[area.Region] = area.WarningCodes.Max() switch
			{
				1 => GetColor("AshfallLight"),
				2 => GetColor("AshfallModerate"),
				3 => GetColor("AshfallHeavy"),
				4 => GetColor("SmallVolcanicBombFall"),
				_ => GetColor("AshfallLight"),
			};

			if (cityLayer != null)
			{
				foreach (var cityPoly in cityLayer.FindPolygon(area.Region))
				{
					zoomPoints.Add((cityPoly.BoundingBox.TopLeft - size).CastLocation());
					zoomPoints.Add((cityPoly.BoundingBox.BottomRight + size).CastLocation());
				}
			}
		}
		MapDisplayParameter = MapDisplayParameter with
		{
			CustomColorMap = new() {
				{ LandLayerType.MunicipalityWeatherWarningArea, map},
			},
		};

		if (zoomPoints.Count != 0)
			MapNavigationRequest = new(zoomPoints.CalcRect());
		else
			MapNavigationRequest = null;
	}

	private SKColor GetColor(string key)
	{
		var avaloniaColor = (Color)(KyoshinEewViewerApp.Application?.FindResource(key) ?? throw new NullReferenceException($"リソース {key} を取得できません"));
		return new(avaloniaColor.R, avaloniaColor.G, avaloniaColor.B, avaloniaColor.A);
	}
}
