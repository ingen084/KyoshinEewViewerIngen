using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Jma;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Data;
using KyoshinEewViewer.Series.Qzss.Layers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Series.Qzss.Models;

public partial class FloodReportGroup : DCReportGroup
{
	public static readonly string TYPE = "Flood";
	public override string Type => TYPE;

	private MapData? MapData { get; }
	private FloodLayer Layer { get; }

	private List<FloodReport> Reports { get; } = [];

	[ObservableProperty]
	public partial int TotalAreaCount { get; set; }

	public record FloodArea(long Region, byte WarningType);
	public ObservableCollection<FloodArea> Regions { get; } = [];

	public FloodReportGroup(FloodReport report, MapData? mapData)
	{
		MapData = mapData;
		Layer = new() { Map = mapData };

		Classification = report.ReportClassification;
		InformationType = report.InformationType;

		ReportTime = ApplyTimezoneOffset(report.ReportTime);
		TotalAreaCount = report.Regions.Count(a => a.Region != 0);

		Reports.Add(report);

		AggregateRegions();
	}

	public override bool CheckDuplicate(DCReport report) => report is FloodReport f && Reports.Any(r => f.Content.SequenceEqual(r.Content));
	public override bool TryProcess(DCReport report)
	{
		if (report is not FloodReport f || ApplyTimezoneOffset(f.ReportTime) != ReportTime)
			return false;

		Reports.Add(f);
		ReportCount++;
		TotalAreaCount += f.Regions.Count(a => a.Region != 0);

		AggregateRegions();
		return true;
	}

	[JsonIgnore]
	public override Control? DetailDisplayControl => new FloodReportControl { DataContext = this };

	public void AggregateRegions()
	{
		Regions.Clear();
		var regions = new List<FloodArea>();

		foreach (var report in Reports)
		{
			foreach (var r in report.Regions)
			{
				if (r.Region == 0)
					continue;
				if (regions.Any(a => a.Region == r.Region && a.WarningType == r.Level))
					continue;
				regions.Add(new FloodArea(r.Region, r.Level));
			}
		}

		regions.Sort((a, b) => a.Region.CompareTo(b.Region));
		foreach (var region in regions)
			Regions.Add(region);

		UpdateMapDisplay();
	}

	private void UpdateMapDisplay()
	{
		var rivers = new Dictionary<long, byte>();
		foreach (var region in Regions)
		{
			// 同じ河川に複数の情報が含まれる場合は深刻なほうを採用する
			if (rivers.TryGetValue(region.Region, out var exist) && exist >= region.WarningType)
				continue;
			rivers[region.Region] = region.WarningType;
		}
		Layer.Rivers = rivers;

		MapDisplayParameter = new()
		{
			// 左側に表示する対象河川の一覧と地図が重ならないようにする
			Padding = new(355, 0, 0, 0),
			OverlayLayers = [Layer],
		};

		// 都道府県･地方単位の「その他河川」には形状が無いため、地図データに形状がある河川だけで表示範囲を決める
		FeatureLayer? riverLayer = null;
		MapData?.TryGetLayer(LandLayerType.DesignatedRiver, out riverLayer);
		var zoomPoints = new List<KyoshinMonitorLib.Location>();
		if (riverLayer != null)
		{
			foreach (var code in rivers.Keys)
			{
				foreach (var p in riverLayer.FindPolygon(code))
				{
					zoomPoints.Add(p.BoundingBox.TopLeft.CastLocation());
					zoomPoints.Add(p.BoundingBox.BottomRight.CastLocation());
				}
			}
		}

		if (zoomPoints.Count <= 0)
		{
			MapNavigationRequest = null;
			return;
		}

		// 河川は上流から下流まで長さがあるため、対象の河川がすべて入る範囲を表示する
		var padding = .1;
		MapNavigationRequest = new(new Rect(
			new Point(zoomPoints.Min(p => p.Latitude) - padding, zoomPoints.Min(p => p.Longitude) - padding),
			new Point(zoomPoints.Max(p => p.Latitude) + padding, zoomPoints.Max(p => p.Longitude) + padding)
		));
	}
}
