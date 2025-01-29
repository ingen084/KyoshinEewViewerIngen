using Avalonia.Controls;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Jma;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace KyoshinEewViewer.Series.Qzss.Models;

public class AshFallReportGroup : DCReportGroup
{
	public List<AshFallReport> Reports { get; } = [];

	private DateTime _reportTime;
	public DateTime ReportTime
	{
		get => _reportTime;
		set => this.RaiseAndSetIfChanged(ref _reportTime, value);
	}

	private DateTime _activityTime;
	public DateTime ActivityTime
	{
		get => _activityTime;
		set => this.RaiseAndSetIfChanged(ref _activityTime, value);
	}

	private int _totalAreaCount;
	public int TotalAreaCount
	{
		get => _totalAreaCount;
		set => this.RaiseAndSetIfChanged(ref _totalAreaCount, value);
	}

	private int _volcanoNameCode;
	public int VolcanoNameCode
	{
		get => _volcanoNameCode;
		set => this.RaiseAndSetIfChanged(ref _volcanoNameCode, value);
	}

	private byte _warningType;
	public byte WarningType
	{
		get => _warningType;
		set => this.RaiseAndSetIfChanged(ref _warningType, value);
	}

	public record AshFallArea(byte WarningCode, int Region);
	public record AshFallTime(DateTime Time, byte ExpectedTime, List<AshFallArea> Areas);

	public ObservableCollection<AshFallTime> AshFallTimes { get; } = [];

	public AshFallReportGroup(AshFallReport report)
	{
		Classification = report.ReportClassification;
		InformationType = report.InformationType;

		ReportTime = report.ReportTime.LocalDateTime;
		TotalAreaCount = report.Regions.Count(a => a.Region != 0);
		VolcanoNameCode = report.VolcanoNameCode;
		ActivityTime = report.ActivityTime.LocalDateTime;
		WarningType = report.WarningType;

		Reports.Add(report);
		UpdateDetails();
	}

	public override bool CheckDuplicate(DCReport report) => report is AshFallReport a && Reports.Any(r => a.Content.SequenceEqual(r.Content));
	public override bool TryProcess(DCReport report)
	{
		if (report is not AshFallReport a || a.ReportTime.LocalDateTime != ReportTime || a.VolcanoNameCode != VolcanoNameCode || a.WarningType != WarningType)
			return false;

		Reports.Add(a);
		ReportCount++;
		TotalAreaCount += a.Regions.Count(a => a.Region != 0);

		UpdateDetails();
		return true;
	}

	public override Control? DetailDisplayControl => new AshFallReportControl { DataContext = this };

	private void UpdateDetails()
	{
		var ashFallTimes = new List<AshFallTime>();
		foreach (var report in Reports)
		{
			foreach (var (expectedTime, warningCode, region) in report.Regions)
			{
				if (expectedTime == 0)
					continue;
				var ashFallTime = ashFallTimes.FirstOrDefault(a => a.ExpectedTime == expectedTime);
				if (ashFallTime == null)
				{
					ashFallTime = new(ActivityTime.AddHours(expectedTime), expectedTime, []);
					ashFallTimes.Add(ashFallTime);
				}
				ashFallTime.Areas.Add(new(warningCode, region));
			}
		}

		AshFallTimes.Clear();
		foreach (var t in ashFallTimes.OrderBy(a => a.Time))
			AshFallTimes.Add(t);
	}
}
