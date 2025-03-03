using Avalonia.Controls;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Jma;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace KyoshinEewViewer.Series.Qzss.Models;

public class MarineReportGroup : DCReportGroup
{
	public List<MarineReport> Reports { get; } = [];

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

	public record MarineWarningRegion(int AreaCode, List<byte> WarningCodes);
	private ObservableCollection<MarineWarningRegion> _aggregatedRegions = [];
	public ObservableCollection<MarineWarningRegion> AggregatedRegions
	{
		get => _aggregatedRegions;
		set => this.RaiseAndSetIfChanged(ref _aggregatedRegions, value);
	}

	public MarineReportGroup(MarineReport report)
	{
		Classification = report.ReportClassification;
		InformationType = report.InformationType;

		ReportTime = report.ReportTime.LocalDateTime;
		AggregateRegions(report);
		TotalAreaCount = AggregatedRegions.Count;

		Reports.Add(report);
	}

	public override bool CheckDuplicate(DCReport report) => report is MarineReport m && Reports.Any(r => m.Content.SequenceEqual(r.Content));
	public override bool TryProcess(DCReport report)
	{
		if (report is not MarineReport m || m.ReportTime.LocalDateTime != ReportTime)
			return false;

		Reports.Add(m);
		ReportCount++;
		AggregateRegions(m);
		TotalAreaCount = AggregatedRegions.Count;
		return true;
	}

	private void AggregateRegions(MarineReport report)
	{
		foreach (var (WarningCode, Region) in report.Regions)
		{
			if (Region == 0)
				continue;

			if (AggregatedRegions.FirstOrDefault(r => r.AreaCode == Region) is { } value)
				value.WarningCodes.Add(WarningCode);
			else
				AggregatedRegions.Add(new(Region, [WarningCode]));
		}
		this.RaisePropertyChanged(nameof(AggregatedRegions));
	}

	public override Control? DetailDisplayControl => new MarineReportControl { DataContext = this };
}
