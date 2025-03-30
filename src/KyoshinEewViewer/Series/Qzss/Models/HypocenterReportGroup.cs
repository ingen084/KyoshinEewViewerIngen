using Avalonia.Controls;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Jma;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Layers;
using ReactiveUI;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using WindowTheme = KyoshinEewViewer.Core.Models.WindowTheme;
using KyoshinEewViewer.Core.Models.Events;
using Avalonia;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Series.Qzss.Models;

public class HypocenterReportGroup : DCReportGroup
{
	public static readonly string TYPE = "Hypocenter";
	public override string Type => TYPE;

	private List<HypocenterReport> Reports { get; } = [];

	private DateTime _reportTime;
	public DateTime ReportTime
	{
		get => _reportTime;
		set => this.RaiseAndSetIfChanged(ref _reportTime, value);
	}

	private DateTime _occurrenceTime;
	public DateTime OccurrenceTime
	{
		get => _occurrenceTime;
		set => this.RaiseAndSetIfChanged(ref _occurrenceTime, value);
	}

	private byte _rawMagnitude;
	public byte RawMagnitude
	{
		get => _rawMagnitude;
		set => this.RaiseAndSetIfChanged(ref _rawMagnitude, value);
	}
	public float Magnitude => RawMagnitude * 0.1f;
	public string? MagnitudeAltString => RawMagnitude switch
	{
		101 => "10.1以上",
		127 => "不明",
		_ => null
	};

	private int _rawDepth;
	public int RawDepth
	{
		get => _rawDepth;
		set => this.RaiseAndSetIfChanged(ref _rawDepth, value);
	}
	public string? DepthAltString => RawDepth switch
	{
		0 => "ごく浅い",
		511 => "不明",
		501 => "500km以上",
		_ => null
	};

	private int _epicenter;
	public int Epicenter
	{
		get => _epicenter;
		set => this.RaiseAndSetIfChanged(ref _epicenter, value);
	}

	private string _comments = "";
	public string Comments
	{
		get => _comments;
		set => this.RaiseAndSetIfChanged(ref _comments, value);
	}

	public HypocenterReportGroup(HypocenterReport report)
	{
		Classification = report.ReportClassification;
		InformationType = report.InformationType;

		ReportTime = report.ReportTime.LocalDateTime;
		OccurrenceTime = report.OccurrenceTime.LocalDateTime;
		RawMagnitude = report.Magnitude;
		RawDepth = report.Depth;
		Epicenter = report.Epicenter;

		var comments = new List<string>();
		foreach (var i in report.Information)
		{
			if (i == 0)
				continue;
			comments.Add(CsvDictionary.AdditionalCommentEarthquake.TryGetValue(i, out var c) ? c : $"不明なコメント({i})");
		}
		Comments = string.Join('\n', comments);

		var loc = new KyoshinMonitorLib.Location(report.Latitude, report.Longitude);
		var size = 2;
		MapNavigationRequest = new MapNavigationRequest(new(
			new Point(loc.Latitude - size, loc.Longitude - size),
			new Point(loc.Latitude + size, loc.Longitude + size)
		));

		MapDisplayParameter = new() {
			OverlayLayers = [
				new HypocenterLayer(loc)
			],
		};

		Reports.Add(report);
	}

	public override bool CheckDuplicate(DCReport report) => report is HypocenterReport h && Reports.Any(r => h.Content.SequenceEqual(r.Content));
	public override bool TryProcess(DCReport report) => false;

	[JsonIgnore]
	public override Control? DetailDisplayControl => new HypocenterReportControl { DataContext = this };
}

public class HypocenterLayer(KyoshinMonitorLib.Location location) : MapLayer
{
	private SKPaint HypocenterBorderPen { get; } = new SKPaint
	{
		Style = SKPaintStyle.Stroke,
		Color = new SKColor(255, 255, 0, 255),
		StrokeWidth = 8,
		IsAntialias = true,
	};

	private SKPaint HypocenterBodyPen { get; } = new SKPaint
	{
		Style = SKPaintStyle.Stroke,
		Color = new SKColor(255, 0, 0, 255),
		StrokeWidth = 6,
		IsAntialias = true,
	};

	public KyoshinMonitorLib.Location Location { get; } = location;

	public override bool NeedPersistentUpdate => false;

	public override void RefreshResourceCache(WindowTheme windowTheme)
	{
		HypocenterBorderPen.Color = SKColor.Parse(windowTheme.EarthquakeHypocenterBorderColor);
		HypocenterBodyPen.Color = SKColor.Parse(windowTheme.EarthquakeHypocenterColor);
	}

	public override void Render(SKCanvas canvas, LayerRenderParameter param, bool isAnimating)
	{
		canvas.Save();
		try
		{
			var zoom = param.Zoom;
			canvas.Translate((float)-param.LeftTopPixel.X, (float)-param.LeftTopPixel.Y);

			var largeMinSize = 10 + (zoom - 5) * 1.25;
			var largeMaxSize = largeMinSize + 1;
			var smallMinSize = 6 + (zoom - 5) * 1.25;

			var basePoint = Location.ToPixel(zoom);
			canvas.DrawLine((basePoint - new PointD(largeMaxSize, largeMaxSize)).AsSkPoint(), (basePoint + new PointD(largeMaxSize, largeMaxSize)).AsSkPoint(), HypocenterBorderPen);
			canvas.DrawLine((basePoint - new PointD(-largeMaxSize, largeMaxSize)).AsSkPoint(), (basePoint + new PointD(-largeMaxSize, largeMaxSize)).AsSkPoint(), HypocenterBorderPen);

			canvas.DrawLine((basePoint - new PointD(largeMinSize, largeMinSize)).AsSkPoint(), (basePoint + new PointD(largeMinSize, largeMinSize)).AsSkPoint(), HypocenterBodyPen);
			canvas.DrawLine((basePoint - new PointD(-largeMinSize, largeMinSize)).AsSkPoint(), (basePoint + new PointD(-largeMinSize, largeMinSize)).AsSkPoint(), HypocenterBodyPen);
		}
		finally
		{
			canvas.Restore();
		}

	}
}
