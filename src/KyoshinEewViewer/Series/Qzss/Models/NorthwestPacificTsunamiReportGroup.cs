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

public record NPTsunamiArea(byte Code, string Status, string Height);
public partial class NorthwestPacificTsunamiReportGroup : DCReportGroup
{
	public static readonly string TYPE = "NorthwestPacificTsunami";
	public override string Type => TYPE;

	private List<NorthwestPacificTsunamiReport> Reports { get; } = [];

	[ObservableProperty]
	public partial int TotalAreaCount { get; set; }

	[ObservableProperty]
	public partial byte TsunamigenicPotential { get; set; }

	public ObservableCollection<NPTsunamiArea> Areas { get; } = [];

	public NorthwestPacificTsunamiReportGroup(NorthwestPacificTsunamiReport report)
	{
		Classification = report.ReportClassification;
		InformationType = report.InformationType;

		ReportTime = ApplyTimezoneOffset(report.ReportTime);
		TotalAreaCount = report.Regions.Count(a => a.Region != 0);
		TsunamigenicPotential = report.TsunamigenicPotential;

		Reports.Add(report);
		UpdateDetails();
	}

	public override bool CheckDuplicate(DCReport report) => report is NorthwestPacificTsunamiReport n && Reports.Any(r => n.Content.SequenceEqual(r.Content));
	public override bool TryProcess(DCReport report)
	{
		if (report is not NorthwestPacificTsunamiReport n || ApplyTimezoneOffset(n.ReportTime) != ReportTime || n.TsunamigenicPotential != TsunamigenicPotential)
			return false;

		Reports.Add(n);
		ReportCount++;
		TotalAreaCount += n.Regions.Count(a => a.Region != 0);

		UpdateDetails();
		return true;
	}

	[JsonIgnore]
	public override Control? DetailDisplayControl => new NorthwestPacificTsunamiReportControl { DataContext = this };

	private static string GetTsunamiHeightString(int height)
		=> height switch
		{
			1 => "0.3m~1m",
			2 => "1m~3m",
			3 => "3m~5m",
			4 => "5m~10m",
			508 => "10m超",
			509 => "巨大",
			510 => "高い",
			511 => "不明",
			_ => $"不明({height})",
		};
	public void UpdateDetails()
	{
		Areas.Clear();
		// 位置が分かっている予報地点のみ地図に表示する
		var points = new Dictionary<byte, NPTsunamiPoint>();
		foreach (var report in Reports)
		{
			foreach (var area in report.Regions)
			{
				if (area.Region == 0)
					continue;
				Areas.Add(new(area.Region, area.IsArrived ? "到達" : ApplyTimezoneOffset(area.ArrivalTime).ToString("HH:mm 到達見込み"), GetTsunamiHeightString(area.Height)));

				if (!CsvDictionary.DCRNorthwestPacificTsunamiLocation.TryGetValue(area.Region, out var location))
					continue;
				// 同じ地点が複数の電文に含まれる場合は高いほうを採用する
				if (points.TryGetValue(area.Region, out var exist) && NorthwestPacificTsunamiLayer.GetHeightRank(exist.Height) >= NorthwestPacificTsunamiLayer.GetHeightRank(area.Height))
					continue;
				points[area.Region] = new(new Location(location.Latitude, location.Longitude), area.Height);
			}
		}

		if (points.Count <= 0)
			return;

		MapDisplayParameter = new()
		{
			OverlayLayers = [new NorthwestPacificTsunamiLayer([.. points.Values])],
		};

		// 予報地点は北西太平洋全域に散らばるため、対象の地点がすべて入る範囲を表示する
		var padding = 1;
		MapNavigationRequest = new(new(
			new Point(points.Values.Min(p => p.Location.Latitude) - padding, points.Values.Min(p => p.Location.Longitude) - padding),
			new Point(points.Values.Max(p => p.Location.Latitude) + padding, points.Values.Max(p => p.Location.Longitude) + padding)
		));
	}
}

public record NPTsunamiPoint(Location Location, int Height);

/// <summary>
/// 北西太平洋津波の予報地点を表示するレイヤー
/// </summary>
public class NorthwestPacificTsunamiLayer(NPTsunamiPoint[] points) : MapLayer
{
	private SKPaint BorderPaint { get; } = new SKPaint
	{
		Style = SKPaintStyle.Stroke,
		StrokeWidth = 3,
		IsAntialias = true,
	};

	private SKPaint MajorWarningPaint { get; } = new SKPaint
	{
		Style = SKPaintStyle.Fill,
		IsAntialias = true,
	};

	private SKPaint WarningPaint { get; } = new SKPaint
	{
		Style = SKPaintStyle.Fill,
		IsAntialias = true,
	};

	private SKPaint AdvisoryPaint { get; } = new SKPaint
	{
		Style = SKPaintStyle.Fill,
		IsAntialias = true,
	};

	private SKPaint UnknownPaint { get; } = new SKPaint
	{
		Style = SKPaintStyle.Fill,
		IsAntialias = true,
	};

	/// <summary>
	/// 地点が重なった場合に高いほうが隠れないよう、低いものから並べておく
	/// </summary>
	public NPTsunamiPoint[] Points { get; } = [.. points.OrderBy(p => GetHeightRank(p.Height))];

	public override bool NeedPersistentUpdate => false;

	/// <summary>
	/// 予想される津波の高さの深刻さ。同じ地点が複数回現れた場合の採用と、描画順に使用する
	/// </summary>
	public static int GetHeightRank(int height)
		=> height switch
		{
			509 => 6, // 巨大
			508 => 5, // 10m超
			4 => 4, // 5m~10m
			3 => 3, // 3m~5m
			2 or 510 => 2, // 1m~3m / 高い
			1 => 1, // 0.3m~1m
			_ => 0, // 不明
		};

	// 高さの区分は国内の津波警報･注意報の区分に合わせる
	private SKPaint GetPaint(int height)
		=> height switch
		{
			3 or 4 or 508 or 509 => MajorWarningPaint,
			2 or 510 => WarningPaint,
			1 => AdvisoryPaint,
			_ => UnknownPaint,
		};

	public override void RefreshResourceCache(WindowTheme windowTheme)
	{
		// 地図のどの色の上でも輪郭が見えるように、背景色で縁取りする
		BorderPaint.Color = SKColor.Parse(windowTheme.MainBackgroundColor);
		MajorWarningPaint.Color = SKColor.Parse(windowTheme.TsunamiMajorWarningColor);
		WarningPaint.Color = SKColor.Parse(windowTheme.TsunamiWarningColor);
		AdvisoryPaint.Color = SKColor.Parse(windowTheme.TsunamiAdvisoryColor);
		UnknownPaint.Color = SKColor.Parse(windowTheme.SubForegroundColor);
	}

	public override void Render(SKCanvas canvas, LayerRenderParameter param, bool isAnimating)
	{
		canvas.Save();
		try
		{
			canvas.Translate((float)-param.LeftTopPixel.X, (float)-param.LeftTopPixel.Y);

			var size = (float)Math.Max(4, 8 + (param.Zoom - 5) * 1.25);
			foreach (var point in Points)
			{
				var basePoint = point.Location.ToPixel(param.Zoom);
				var x = (float)basePoint.X;
				var y = (float)basePoint.Y;

				canvas.DrawCircle(x, y, size, BorderPaint);
				canvas.DrawCircle(x, y, size, GetPaint(point.Height));
			}
		}
		finally
		{
			canvas.Restore();
		}
	}
}
