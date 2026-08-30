using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Jma;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Layers;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;
using Location = KyoshinMonitorLib.Location;
using WindowTheme = KyoshinEewViewer.Core.Models.WindowTheme;

namespace KyoshinEewViewer.Series.Qzss.Models;

public partial class VolcanoReportGroup : DCReportGroup
{
	public static readonly string TYPE = "Volcano";
	public override string Type => TYPE;

	private List<VolcanoReport> Reports { get; } = [];

	[ObservableProperty]
	public partial int TotalAreaCount { get; set; }

	[ObservableProperty]
	public partial int VolcanoNameCode { get; set; }

	[ObservableProperty]
	public partial byte WarningCode { get; set; }

	[ObservableProperty]
	public partial byte Ambiguity { get; set; }

	[ObservableProperty]
	public partial DateTime ActivityTime { get; set; }
	public string ActivityDateString => Ambiguity switch
	{
		>= 0 and <= 4 => ActivityTime.ToString("d日"),
		6 or 7 => "不明(有効時刻なし)",
		_ => "",
	};
	public string ActivityTimeString => Ambiguity switch
	{
		4 => ActivityTime.ToString("HH時"),
		5 => ActivityTime.ToString("d日"),
		6 or 7 => "",
		_ => ActivityTime.ToString("HH時mm分"),
	};

	public ObservableCollection<int> Regions { get; } = [];

	public VolcanoReportGroup(VolcanoReport report)
	{
		Classification = report.ReportClassification;
		InformationType = report.InformationType;

		ReportTime = ApplyTimezoneOffset(report.ReportTime);
		ActivityTime = ApplyTimezoneOffset(report.ActivityTime);
		TotalAreaCount = report.Regions.Count(a => a != 0);
		VolcanoNameCode = report.VolcanoNameCode;
		WarningCode = report.WarningCode;

		Reports.Add(report);

		foreach (var r in report.Regions)
			if (r != 0)
				Regions.Add(r);

		// 位置が分かっている火山のみ地図に表示する
		if (CsvDictionary.PointVolcanoLocation.TryGetValue(VolcanoNameCode, out var volcanoLocation))
		{
			var location = new Location(volcanoLocation.Latitude, volcanoLocation.Longitude);
			MapDisplayParameter = new()
			{
				OverlayLayers = [new VolcanoLayer(location)],
				Padding = new Thickness(245, 0, 0, 0),
			};

			// 火山の警報は周辺の市町村が対象になるため、震源よりも狭い範囲を表示する
			var size = 1;
			MapNavigationRequest = new(new(
				new Point(location.Latitude - size, location.Longitude - size),
				new Point(location.Latitude + size, location.Longitude + size)
			));
		}
	}

	public override bool CheckDuplicate(DCReport report) => report is VolcanoReport v && Reports.Any(r => v.Content.SequenceEqual(r.Content));
	public override bool TryProcess(DCReport report)
	{
		if (report is not VolcanoReport v || ApplyTimezoneOffset(v.ReportTime) != ReportTime || v.VolcanoNameCode != VolcanoNameCode || v.WarningCode != WarningCode)
			return false;

		Reports.Add(v);
		ReportCount++;
		TotalAreaCount += v.Regions.Distinct().Count(a => a != 0);

		foreach (var r in v.Regions)
			if (r != 0)
				Regions.Add(r);

		return true;
	}

	[JsonIgnore]
	public override Control? DetailDisplayControl => new VolcanoReportControl { DataContext = this };
}

/// <summary>
/// 火山の位置を表示するレイヤー
/// </summary>
public class VolcanoLayer(Location location) : MapLayer
{
	/// <summary>
	/// 火山アイコン
	/// </summary>
	private const string VolcanoGlyph = "\uf770";

	private SKPaint BorderPaint { get; } = new SKPaint
	{
		Style = SKPaintStyle.Stroke,
		StrokeWidth = 3,
		StrokeJoin = SKStrokeJoin.Round,
		IsAntialias = true,
	};

	private SKPaint BodyPaint { get; } = new SKPaint
	{
		Style = SKPaintStyle.Fill,
		IsAntialias = true,
	};

	private SKFont IconFont { get; } = new(KyoshinEewViewerFonts.Icon);

	public Location Location { get; } = location;

	public override bool NeedPersistentUpdate => false;

	public override void RefreshResourceCache(WindowTheme windowTheme)
	{
		BorderPaint.Color = windowTheme.IsDark ? SKColors.Black : SKColors.White;
		BodyPaint.Color = SKColor.Parse(windowTheme.ForegroundColor);
	}

	public override void Render(SKCanvas canvas, LayerRenderParameter param, bool isAnimating)
	{
		canvas.Save();
		try
		{
			canvas.Translate((float)-param.LeftTopPixel.X, (float)-param.LeftTopPixel.Y);

			// 火山アイコンをズームに応じた大きさで、中心が火山の位置に重なるように描画する
			IconFont.Size = (float)Math.Max(12, 20 + (param.Zoom - 5) * 2.5);
			using var path = IconFont.GetTextPath(VolcanoGlyph);
			var bounds = path.TightBounds;
			var basePoint = Location.ToPixel(param.Zoom);
			path.Offset((float)basePoint.X - bounds.MidX, (float)basePoint.Y - bounds.MidY);

			canvas.DrawPath(path, BorderPaint);
			canvas.DrawPath(path, BodyPaint);
		}
		finally
		{
			canvas.Restore();
		}
	}
}
