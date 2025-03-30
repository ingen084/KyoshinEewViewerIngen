using Avalonia.Controls;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Jma;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Series.Qzss.Models;

public class FloodReportGroup : DCReportGroup
{
	public static readonly string TYPE = "Flood";
	public override string Type => TYPE;

	private List<FloodReport> Reports { get; } = [];

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

	public record FloodArea(long Region, byte WarningType);
	public ObservableCollection<FloodArea> Regions { get; } = [];

	public FloodReportGroup(FloodReport report)
	{
		Classification = report.ReportClassification;
		InformationType = report.InformationType;

		ReportTime = report.ReportTime.LocalDateTime;
		TotalAreaCount = report.Regions.Count(a => a.Region != 0);

		Reports.Add(report);

		AggregateRegions();
	}

	public override bool CheckDuplicate(DCReport report) => report is FloodReport f && Reports.Any(r => f.Content.SequenceEqual(r.Content));
	public override bool TryProcess(DCReport report)
	{
		if (report is not FloodReport f || f.ReportTime.LocalDateTime != ReportTime)
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
	}
}
