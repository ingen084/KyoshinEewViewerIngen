using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Jma;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Layers;
using KyoshinEewViewer.Series.Qzss.Services;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;
using Location = KyoshinMonitorLib.Location;
using WindowTheme = KyoshinEewViewer.Core.Models.WindowTheme;

namespace KyoshinEewViewer.Series.Qzss.Models;

public partial class FloodReportGroup : DCReportGroup
{
	public static readonly string TYPE = "Flood";
	public override string Type => TYPE;

	private List<FloodReport> Reports { get; } = [];

	[ObservableProperty]
	public partial int TotalAreaCount { get; set; }

	public record FloodArea(long Region, byte WarningType);
	public ObservableCollection<FloodArea> Regions { get; } = [];

	public FloodReportGroup(FloodReport report)
	{
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
		// 都道府県･地方単位の「その他河川」には形状が無いため、形状が分かっている河川のみ地図に表示する
		var rivers = new Dictionary<long, FloodRiver>();
		foreach (var region in Regions)
		{
			if (!FloodForecastRiverService.TryGetRiver(region.Region, out var parts))
				continue;
			// 同じ河川に複数の情報が含まれる場合は深刻なほうを採用する
			if (rivers.TryGetValue(region.Region, out var exist) && exist.WarningType >= region.WarningType)
				continue;
			rivers[region.Region] = new(parts, region.WarningType);
		}

		if (rivers.Count <= 0)
			return;

		MapDisplayParameter = new()
		{
			OverlayLayers = [new FloodLayer([.. rivers.Values])],
		};

		// 河川は上流から下流まで長さがあるため、対象の河川がすべて入る範囲を表示する
		var points = rivers.Values.SelectMany(r => r.Parts).SelectMany(p => p).ToArray();
		var padding = .1;
		MapNavigationRequest = new(new(
			new Point(points.Min(p => p.Latitude) - padding, points.Min(p => p.Longitude) - padding),
			new Point(points.Max(p => p.Latitude) + padding, points.Max(p => p.Longitude) + padding)
		));
	}
}

public record FloodRiver(Location[][] Parts, byte WarningType);

/// <summary>
/// 指定河川洪水予報の対象河川を表示するレイヤー
/// </summary>
public class FloodLayer(FloodRiver[] rivers) : MapLayer
{
	private SKPaint BorderPaint { get; } = new SKPaint
	{
		Style = SKPaintStyle.Stroke,
		StrokeCap = SKStrokeCap.Round,
		StrokeJoin = SKStrokeJoin.Round,
		IsAntialias = true,
	};

	private SKPaint CancelPaint { get; } = CreateLinePaint();
	private SKPaint AdvisoryPaint { get; } = CreateLinePaint();
	private SKPaint WarningPaint { get; } = CreateLinePaint();
	private SKPaint MajorWarningPaint { get; } = CreateLinePaint();

	private static SKPaint CreateLinePaint() => new()
	{
		Style = SKPaintStyle.Stroke,
		StrokeCap = SKStrokeCap.Round,
		StrokeJoin = SKStrokeJoin.Round,
		IsAntialias = true,
	};

	/// <summary>
	/// 河川が重なった場合に深刻なほうが隠れないよう、軽いものから並べておく
	/// </summary>
	public FloodRiver[] Rivers { get; } = [.. rivers.OrderBy(r => r.WarningType)];

	public override bool NeedPersistentUpdate => false;

	// 色分けは一覧表示の FloodWarningColor に合わせる (警報解除のみ RefreshResourceCache の理由で異なる)
	private SKPaint GetPaint(byte warningType)
		=> warningType switch
		{
			2 => AdvisoryPaint,
			3 => WarningPaint,
			4 => MajorWarningPaint,
			_ => CancelPaint,
		};

	public override void RefreshResourceCache(WindowTheme windowTheme)
	{
		// 地図のどの色の上でも輪郭が見えるように縁取りする
		BorderPaint.Color = windowTheme.IsDark ? SKColors.Black : SKColors.White;
		// 一覧では警報解除に DockTitleBackgroundColor を使っているが、
		// パネルの背景色のため地図に塗ると地形とほとんど区別が付かない。
		// 他のレベルは色で判別できるのに対し警報解除は無彩色なので、前景色側を使う
		CancelPaint.Color = SKColor.Parse(windowTheme.SubForegroundColor);
		AdvisoryPaint.Color = SKColor.Parse(windowTheme.TsunamiAdvisoryColor);
		WarningPaint.Color = SKColor.Parse(windowTheme.TsunamiWarningColor);
		MajorWarningPaint.Color = SKColor.Parse(windowTheme.TsunamiMajorWarningColor);
	}

	public override void Render(SKCanvas canvas, LayerRenderParameter param, bool isAnimating)
	{
		canvas.Save();
		try
		{
			canvas.Translate((float)-param.LeftTopPixel.X, (float)-param.LeftTopPixel.Y);

			var width = (float)Math.Max(2, 3 + (param.Zoom - 5) * .8);
			BorderPaint.StrokeWidth = width + 3;

			foreach (var river in Rivers)
			{
				using var path = new SKPath();
				foreach (var part in river.Parts)
				{
					if (part.Length < 2)
						continue;
					for (var i = 0; i < part.Length; i++)
					{
						var point = part[i].ToPixel(param.Zoom);
						if (i == 0)
							path.MoveTo((float)point.X, (float)point.Y);
						else
							path.LineTo((float)point.X, (float)point.Y);
					}
				}
				if (path.IsEmpty)
					continue;

				var paint = GetPaint(river.WarningType);
				paint.StrokeWidth = width;
				canvas.DrawPath(path, BorderPaint);
				canvas.DrawPath(path, paint);
			}
		}
		finally
		{
			canvas.Restore();
		}
	}
}
