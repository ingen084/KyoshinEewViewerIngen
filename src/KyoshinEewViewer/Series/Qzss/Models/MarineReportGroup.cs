using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;
using Avalonia.Controls;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Jma;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Data;
using KyoshinEewViewer.Series.Qzss.Layers;

namespace KyoshinEewViewer.Series.Qzss.Models;

public partial class MarineReportGroup : DCReportGroup
{
	public static readonly string TYPE = "Marine";
	public override string Type => TYPE;

	private MapData? MapData { get; }
	private MarineWarningLayer Layer { get; }
	private MarineWarningIconLayer IconLayer { get; }
	private List<MarineReport> Reports { get; } = [];

	[ObservableProperty]
	public partial int TotalAreaCount { get; set; }

	public record MarineWarningRegion(int AreaCode, List<byte> WarningCodes);
	[ObservableProperty]
	public partial ObservableCollection<MarineWarningRegion> AggregatedRegions { get; set; } = [];

	public MarineReportGroup(MarineReport report, MapData? mapData)
	{
		MapData = mapData;
		Layer = new()
		{
			Current = this,
			Map = mapData
		};
		IconLayer = new()
		{
			Current = this,
			Map = mapData
		};

		Classification = report.ReportClassification;
		InformationType = report.InformationType;

		ReportTime = ApplyTimezoneOffset(report.ReportTime);
		AggregateRegions(report);
		TotalAreaCount = AggregatedRegions.Count;

		Reports.Add(report);

		UpdateMapDisplay();
	}

	public override bool CheckDuplicate(DCReport report) => report is MarineReport m && Reports.Any(r => m.RawData.AsSpan(3, 23).SequenceEqual(r.RawData.AsSpan(3, 23)));
	public override bool TryProcess(DCReport report)
	{
		if (report is not MarineReport m || ApplyTimezoneOffset(m.ReportTime) != ReportTime)
			return false;

		Reports.Add(m);
		ReportCount++;
		AggregateRegions(m);
		TotalAreaCount = AggregatedRegions.Count;

		UpdateMapDisplay();
		return true;
	}

	private void AggregateRegions(MarineReport report)
	{
		foreach (var (WarningCode, Region) in report.Regions)
		{
			if (Region == 0)
				continue;

			if (AggregatedRegions.FirstOrDefault(r => r.AreaCode == Region) is { } value)
				value.WarningCodes.Add(WarningCode);
			else
				AggregatedRegions.Add(new(Region, [WarningCode]));
		}
		OnPropertyChanged(nameof(AggregatedRegions));
	}

	private void UpdateMapDisplay()
	{
		var zoomPoints = new List<KyoshinMonitorLib.Location>();
		FeatureLayer? marineLayer = null;
		MapData?.TryGetLayer(LandLayerType.LocalMarineForecastArea, out marineLayer);

		foreach (var area in AggregatedRegions)
		{
			if (marineLayer != null)
			{
				foreach (var poly in marineLayer.FindPolygon(area.AreaCode))
				{
					zoomPoints.Add(poly.BoundingBox.TopLeft.CastLocation());
					zoomPoints.Add(poly.BoundingBox.BottomRight.CastLocation());
				}
			}
		}

		Layer.RequestRefresh();
		IconLayer.RequestRefresh();

		MapDisplayParameter = new()
		{
			BackgroundLayers = [Layer],
			OverlayLayers = [IconLayer],
			Padding = new(245, 0, 0, 0),
			LayerSets = [
				new(0, LandLayerType.EarthquakeInformationPrefecture),
			],
		};

		if (zoomPoints.Count != 0)
			MapNavigationRequest = new(zoomPoints.CalcRect());
		else
			MapNavigationRequest = null;
	}

	[JsonIgnore]
	public override Control? DetailDisplayControl => new MarineReportControl { DataContext = this };
}
