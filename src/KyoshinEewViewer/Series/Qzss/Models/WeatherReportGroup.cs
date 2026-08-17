using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Jma;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Data;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Series.Qzss.Models;

public partial class WeatherReportGroup : DCReportGroup
{
	public static readonly string TYPE = "Weather";
	public override string Type => TYPE;

	private MapData? MapData { get; }
	private List<WeatherReport> Reports { get; } = [];

	[ObservableProperty]
	public partial int TotalAreaCount { get; set; }

	public record WeatherWarning(byte SubCategory, bool IsCleared);
	public record WeatherWarningArea(int Region, List<WeatherWarning> Warnings);
	public ObservableCollection<WeatherWarningArea> WarningAreas { get; } = [];

	public WeatherReportGroup(WeatherReport report, MapData? mapData)
	{
		MapData = mapData;

		Classification = report.ReportClassification;
		InformationType = report.InformationType;

		ReportTime = ApplyTimezoneOffset(report.ReportTime);

		Reports.Add(report);
		UpdateArea();
	}

	public override bool CheckDuplicate(DCReport report) => report is WeatherReport w && Reports.Any(r => w.Content.SequenceEqual(r.Content));
	public override bool TryProcess(DCReport report)
	{
		if (report is not WeatherReport w || ApplyTimezoneOffset(w.ReportTime) != ReportTime)
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

				var warning = new WeatherWarning(subCategory, isCleared);
				if (!area.Warnings.Contains(warning))
					area.Warnings.Add(warning);
			}
		}

		var zoomPoints = new List<KyoshinMonitorLib.Location>();
		FeatureLayer? cityLayer = null;
		MapData?.TryGetLayer(LandLayerType.PrimarySubdivisionArea, out cityLayer);
		var size = new PointD(.1, .1);

		var map = new Dictionary<int, SKColor>();
		foreach (var area in WarningAreas)
		{
			// 末尾が000の地域は集合地域のため、配下のポリゴンを含む
			var isGroupRegion = area.Region % 1000 == 0;

			// 有効な警報（解除されていないもの）から最も危険度の高い色を採用
			var activeWarnings = area.Warnings.Where(w => !w.IsCleared).ToList();
			SKColor color;
			if (activeWarnings.Count > 0)
			{
				var highestPriority = activeWarnings.MinBy(w => GetWarningPriority(w.SubCategory));
				color = GetColorForSubCategory(highestPriority!.SubCategory);
			}
			else
			{
				color = SKColors.Gray;
			}

			if (!isGroupRegion)
				map[area.Region] = color;

			if (cityLayer != null)
			{
				foreach (var cityPoly in cityLayer.FindPolygon(isGroupRegion ? area.Region / 1000 : area.Region, 1000))
				{
					zoomPoints.Add((cityPoly.BoundingBox.TopLeft - size).CastLocation());
					zoomPoints.Add((cityPoly.BoundingBox.BottomRight + size).CastLocation());
					if (isGroupRegion && cityPoly.Code is { } code)
						map[code] = color;
				}
			}
		}

		TotalAreaCount = WarningAreas.Count;

		MapDisplayParameter = new (){
			Padding = new(285, 0, 0, 0),
			CustomColorMap = new() {
				{ LandLayerType.PrimarySubdivisionArea, map },
			},
			LayerSets = [new(0, LandLayerType.PrimarySubdivisionArea)],
		};
		if (zoomPoints.Count != 0)
			MapNavigationRequest = new(zoomPoints.CalcRect());
		else
			MapNavigationRequest = null;
	}

	private static SKColor GetColor(string key)
	{
		var avaloniaColor = (Color)(KyoshinEewViewerApp.Application?.FindResource(key)
			?? throw new NullReferenceException($"リソース {key} を取得できません"));
		return new SKColor(avaloniaColor.R, avaloniaColor.G, avaloniaColor.B, avaloniaColor.A);
	}

	private static SKColor GetColorForSubCategory(byte subCategory)
		=> subCategory switch
		{
			>= 1 and <= 7 => GetColor("WeatherWarningLevel5Color"),
			21 or 23 => GetColor("WeatherWarningLevel4Color"),
			31 => GetColor("WeatherWarningLevel3Color"),
			22 => GetColor("WeatherWarningLevel2Color"),
			_ => GetColor("WeatherWarningLevel3Color"),
		};

	private static int GetWarningPriority(byte subCategory)
		=> subCategory switch
		{
			>= 1 and <= 7 => 0,
			21 or 23 => 1,
			31 => 2,
			22 => 3,
			_ => 4,
		};
}
