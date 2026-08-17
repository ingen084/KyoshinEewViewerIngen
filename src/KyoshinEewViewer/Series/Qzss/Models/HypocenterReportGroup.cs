using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Jma;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Map.Layers;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using WindowTheme = KyoshinEewViewer.Core.Models.WindowTheme;
using KyoshinEewViewer.Core.Models.Events;
using Avalonia;
using System.Text.Json.Serialization;
using KyoshinEewViewer.Map;
using Location = KyoshinMonitorLib.Location;

namespace KyoshinEewViewer.Series.Qzss.Models;

public partial class HypocenterReportGroup : DCReportGroup
{
	public static readonly string TYPE = "Hypocenter";
	public override string Type => TYPE;

	private List<HypocenterReport> Reports { get; } = [];

	[ObservableProperty]
	public partial DateTime OccurrenceTime { get; set; }

	[ObservableProperty]
	public partial byte RawMagnitude { get; set; }
	public float Magnitude => RawMagnitude * 0.1f;
	public string? MagnitudeAltString => RawMagnitude switch
	{
		101 => "10.1以上",
		127 => "不明",
		_ => null
	};

	[ObservableProperty]
	public partial int RawDepth { get; set; }
	public string? DepthAltString => RawDepth switch
	{
		0 => "ごく浅い",
		511 => "不明",
		501 => "500km以上",
		_ => null
	};

	[ObservableProperty]
	public partial int Epicenter { get; set; }

	[ObservableProperty]
	public partial string Comments { get; set; } = "";

	public HypocenterReportGroup(HypocenterReport report)
	{
		Classification = report.ReportClassification;
		InformationType = report.InformationType;

		ReportTime = ApplyTimezoneOffset(report.ReportTime);
		OccurrenceTime = ApplyTimezoneOffset(report.OccurrenceTime);
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

		var loc = new Location(report.Latitude, report.Longitude);
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

public partial class HypocenterLayer(Location location) : MapLayer
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

	private SKPaint HypocenterRangePen { get; } = new SKPaint
	{
		Style = SKPaintStyle.Stroke,
		Color = new SKColor(255, 0, 0, 255),
		IsAntialias = true,
	};

	public Location Location { get; } = location;

	public override bool NeedPersistentUpdate => false;

	public override void RefreshResourceCache(WindowTheme windowTheme)
	{
		HypocenterBorderPen.Color = SKColor.Parse(windowTheme.EarthquakeHypocenterBorderColor);
		HypocenterBodyPen.Color = SKColor.Parse(windowTheme.EarthquakeHypocenterColor);

		var rangeColor = SKColor.Parse(windowTheme.EarthquakeHypocenterBorderColor);
		HypocenterRangePen.Color = rangeColor.WithAlpha(128);
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

			// 丸め誤差範囲の描画（±0.05度）
			if (zoom >= 8.75)
			{
				var topLeft = new Location(Location.Latitude + 0.05f, Location.Longitude - 0.05f).ToPixel(zoom);
				var bottomRight = new Location(Location.Latitude - 0.05f, Location.Longitude + 0.05f).ToPixel(zoom);
				var rect = new SKRect(
					(float)topLeft.X,
					(float)topLeft.Y,
					(float)bottomRight.X,
					(float)bottomRight.Y
				);

				var widthLength = rect.Width * 0.2f;
				var heightLength = rect.Height * 0.2f;

				// 左上
				canvas.DrawLine(rect.Left, rect.Top, rect.Left + widthLength, rect.Top, HypocenterRangePen);
				canvas.DrawLine(rect.Left, rect.Top, rect.Left, rect.Top + heightLength, HypocenterRangePen);
				// 右上
				canvas.DrawLine(rect.Right, rect.Top, rect.Right - widthLength, rect.Top, HypocenterRangePen);
				canvas.DrawLine(rect.Right, rect.Top, rect.Right, rect.Top + heightLength, HypocenterRangePen);
				// 左下
				canvas.DrawLine(rect.Left, rect.Bottom, rect.Left + widthLength, rect.Bottom, HypocenterRangePen);
				canvas.DrawLine(rect.Left, rect.Bottom, rect.Left, rect.Bottom - heightLength, HypocenterRangePen);
				// 右下
				canvas.DrawLine(rect.Right, rect.Bottom, rect.Right - widthLength, rect.Bottom, HypocenterRangePen);
				canvas.DrawLine(rect.Right, rect.Bottom, rect.Right, rect.Bottom - heightLength, HypocenterRangePen);
			}
		}
		finally
		{
			canvas.Restore();
		}
	}
}
