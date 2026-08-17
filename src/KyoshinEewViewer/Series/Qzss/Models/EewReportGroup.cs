using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Jma;
using KyoshinEewViewer.Map.Data;
using KyoshinEewViewer.Map;
using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;
using System.Text.Json.Serialization;
using KyoshinEewViewer.Core.Models;

namespace KyoshinEewViewer.Series.Qzss.Models;

public partial class EewReportGroup : DCReportGroup
{
	public static readonly string TYPE = "EEW";
	public override string Type => TYPE;

	private readonly int[] EewAreaTable = [
		9011,
		9012,
		9013,
		9014,
		9020,
		9030,
		9040,
		9050,
		9060,
		9070,
		9080,
		9090,
		9100,
		9110,
		9120,
		9131,
		9132,
		9133,
		9140,
		9150,
		9160,
		9170,
		9180,
		9190,
		9200,
		9210,
		9220,
		9230,
		9240,
		9250,
		9260,
		9270,
		9280,
		9290,
		9300,
		9310,
		9320,
		9330,
		9340,
		9350,
		9360,
		9370,
		9380,
		9390,
		9400,
		9410,
		9420,
		9430,
		9440,
		9450,
		9461,
		9462,
		9471,
		9472,
		9473,
		9474,
		9910,
		9920,
		9931,
		9932,
		9933,
		9934,
		9935,
		9936,
		9941,
		9942,
		9943,
		9951,
		9952,
		9960,
		0
	];

	private List<EewReport> Reports { get; } = [];

    [ObservableProperty]
    public partial DateTime OccurrenceTime { get; set; }

    [ObservableProperty]
    public partial int TotalAreaCount { get; set; }

    [ObservableProperty]
    public partial EewSeismicIntensity Intensity { get; set; }

    [ObservableProperty]
    public partial bool IsIntensityOver { get; set; }

    [ObservableProperty]
    public partial byte RawMagnitude { get; set; }
	public float? Magnitude => RawMagnitude switch
	{
		127 => null,
		> 101 => null,
		_ => RawMagnitude / 10f
	};

	[ObservableProperty]
	public partial int Depth { get; set; }

	[ObservableProperty]
	public partial int Epicenter { get; set; }

	[ObservableProperty]
	public partial List<string> WarningRegions { get; set; } = [];

	public bool IsTemporary => RawMagnitude == 10 && Depth == 10;

	public EewReportGroup(EewReport report, MapData? mapData)
    {
        Classification = report.ReportClassification;
        InformationType = report.InformationType;

        ReportTime = ApplyTimezoneOffset(report.ReportTime);
        OccurrenceTime = ApplyTimezoneOffset(report.OccurrenceTime);
        // index 56 以降はまとめられた地域のため無視する
        for (var i = 0; i < 56; i++)
            if (report.WarningRegions[i])
                TotalAreaCount++;
        Intensity = report.SeismicIntensityLowerLimit;
        IsIntensityOver = report.SeismicIntensityUpperLimit == EewSeismicIntensity.Over;
        RawMagnitude = report.Magnitude;
		Depth = report.Depth;
		Epicenter = report.Epicenter;

		Reports.Add(report);

		FeatureLayer? areaLayer = null;
		mapData?.TryGetLayer(LandLayerType.PrefectureForecastAreaForEew, out areaLayer);

		var zoomPoints = new List<KyoshinMonitorLib.Location>();
		var map = new Dictionary<int, SKColor>();
		var size = new PointD(.1, .1);
		for (var i = 0; i < report.WarningRegions.Length; i++)
		{
			if (report.WarningRegions[i])
			{
				var areaCode = EewAreaTable[i];
				map[areaCode] = SKColors.Tomato;
				// 99XX は地域単位のため無視
				if (areaCode / 100 != 99)
					WarningRegions.Add(CsvDictionary.AreaForecastLocalEEW.TryGetValue(EewAreaTable[i], out var name) ? name.Name : $"不明{EewAreaTable[i]}");
				if (areaLayer != null)
				{
					foreach (var poly in areaLayer.FindPolygon(EewAreaTable[i]))
					{
						zoomPoints.Add((poly.BoundingBox.TopLeft - size).CastLocation());
						zoomPoints.Add((poly.BoundingBox.BottomRight + size).CastLocation());
					}
				}
			}
		}

		MapDisplayParameter = MapDisplayParameter with
		{
			CustomColorMap = new()
			{
				{ LandLayerType.PrefectureForecastAreaForEew, map },
			},
			LayerSets = [new(0, LandLayerType.PrefectureForecastAreaForEew)],
		};

		if (zoomPoints.Count != 0)
			MapNavigationRequest = new(zoomPoints.CalcRect());
		else
			MapNavigationRequest = null;
    }

    public override bool CheckDuplicate(DCReport report) => report is EewReport eew && Reports.Any(r => eew.Content.SequenceEqual(r.Content));
    public override bool TryProcess(DCReport report) => false;

	[JsonIgnore]
	public override Control? DetailDisplayControl => new EewReportControl { DataContext = this };
}
