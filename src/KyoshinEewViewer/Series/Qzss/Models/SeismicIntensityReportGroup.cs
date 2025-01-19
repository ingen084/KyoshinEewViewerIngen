using Avalonia.Controls;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Jma;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Data;
using KyoshinMonitorLib;
using ReactiveUI;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Series.Qzss.Models;

public class SeismicIntensityReportGroup : DCReportGroup
{
    public List<SeismicIntensityReport> Reports { get; } = [];

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

    private SeismicIntensity _maxIntensity;
    public SeismicIntensity MaxIntensity
    {
        get => _maxIntensity;
        set => this.RaiseAndSetIfChanged(ref _maxIntensity, value);
    }

	private MapData? MapData { get; set; }

	public SeismicIntensityReportGroup(SeismicIntensityReport report, MapData? mapData)
    {
		MapData = mapData;

		Classification = report.ReportClassification;
        InformationType = report.InformationType;

        ReportTime = report.ReportTime.LocalDateTime;
        TotalAreaCount = report.Regions.Count(a => a.Region != 0);
        MaxIntensity = report.Regions.Max(r => r.Intensity);

        Reports.Add(report);

		UpdateMap();
	}

	public override bool CheckDuplicate(DCReport report) => report is SeismicIntensityReport si && Reports.Any(r => si.Content.SequenceEqual(r.Content));
    public override bool TryProcess(DCReport report)
    {
        if (report is not SeismicIntensityReport si || si.ReportTime.LocalDateTime != ReportTime)
            return false;

        Reports.Add(si);
        ReportCount++;
        TotalAreaCount += si.Regions.Count(a => a.Region != 0);
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

		foreach (var r in Reports)
		{
			foreach (var (i, c) in r.Regions)
			{
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

		MapDisplayParameter = MapDisplayParameter with
		{
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

	public override Control? DetailDisplayControl => null;
}
