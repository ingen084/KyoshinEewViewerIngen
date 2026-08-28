using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.Core.Models;
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
	private SKPaint BorderPaint { get; } = new SKPaint
	{
		Style = SKPaintStyle.Stroke,
		StrokeWidth = 4,
		StrokeJoin = SKStrokeJoin.Round,
		IsAntialias = true,
	};

	private SKPaint BodyPaint { get; } = new SKPaint
	{
		Style = SKPaintStyle.Fill,
		IsAntialias = true,
	};

	public Location Location { get; } = location;

	public override bool NeedPersistentUpdate => false;

	public override void RefreshResourceCache(WindowTheme windowTheme)
	{
		// 地図のどの色の上でも輪郭が見えるように、背景色で縁取りする
		BorderPaint.Color = SKColor.Parse(windowTheme.MainBackgroundColor);
		BodyPaint.Color = SKColor.Parse(windowTheme.EmphasisForegroundColor);
	}

	public override void Render(SKCanvas canvas, LayerRenderParameter param, bool isAnimating)
	{
		canvas.Save();
		try
		{
			canvas.Translate((float)-param.LeftTopPixel.X, (float)-param.LeftTopPixel.Y);

			// 山の形にした三角形をズームに応じた大きさで描画する
			var size = (float)Math.Max(4, 8 + (param.Zoom - 5) * 1.25);
			var basePoint = Location.ToPixel(param.Zoom);
			var x = (float)basePoint.X;
			var y = (float)basePoint.Y;

			using var path = new SKPath();
			path.MoveTo(x, y - size);
			path.LineTo(x + size, y + size * .7f);
			path.LineTo(x - size, y + size * .7f);
			path.Close();

			canvas.DrawPath(path, BorderPaint);
			canvas.DrawPath(path, BodyPaint);
		}
		finally
		{
			canvas.Restore();
		}
	}
}
