using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Jma;
using KyoshinEewViewer.Series.Qzss.Layers;
using KyoshinEewViewer.Series.Qzss.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;
using Location = KyoshinMonitorLib.Location;

namespace KyoshinEewViewer.Series.Qzss.Models;

/// <summary>
/// 地図に表示する対象河川
/// </summary>
/// <param name="Code">洪水予報区のコード</param>
/// <param name="Parts">河川の形状。分岐や中断があるため複数の線分になる</param>
/// <param name="WarningType">電文上の警戒レベル</param>
public record FloodRiver(long Code, Location[][] Parts, byte WarningType);

public partial class FloodReportGroup : DCReportGroup
{
	public static readonly string TYPE = "Flood";
	public override string Type => TYPE;

	private List<FloodReport> Reports { get; } = [];
	private FloodLayer Layer { get; } = new();

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
			rivers[region.Region] = new(region.Region, parts, region.WarningType);
		}

		Layer.Rivers = [.. rivers.Values];

		MapDisplayParameter = new()
		{
			// 左側に表示する対象河川の一覧と地図が重ならないようにする
			Padding = new(355, 0, 0, 0),
			OverlayLayers = [Layer],
		};

		if (rivers.Count <= 0)
		{
			MapNavigationRequest = null;
			return;
		}

		// 河川は上流から下流まで長さがあるため、対象の河川がすべて入る範囲を表示する
		var points = rivers.Values.SelectMany(r => r.Parts).SelectMany(p => p).ToArray();
		var padding = .1;
		MapNavigationRequest = new(new(
			new Point(points.Min(p => p.Latitude) - padding, points.Min(p => p.Longitude) - padding),
			new Point(points.Max(p => p.Latitude) + padding, points.Max(p => p.Longitude) + padding)
		));
	}
}
