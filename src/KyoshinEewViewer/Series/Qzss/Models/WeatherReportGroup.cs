using Avalonia.Controls;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Jma;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Series.Qzss.Models;

public class WeatherReportGroup : DCReportGroup
{
	public List<WeatherReport> Reports { get; } = [];

	private DateTime _reportTime;
	public DateTime ReportTime
	{
		get => _reportTime;
		set => this.RaiseAndSetIfChanged(ref _reportTime, value);
	}

	private byte _warningState;
	public byte WarningState
	{
		get => _warningState;
		set => this.RaiseAndSetIfChanged(ref _warningState, value);
	}

	private int _totalAreaCount;
	public int TotalAreaCount
	{
		get => _totalAreaCount;
		set => this.RaiseAndSetIfChanged(ref _totalAreaCount, value);
	}

	public WeatherReportGroup(WeatherReport report)
	{
		Classification = report.ReportClassification;
		InformationType = report.InformationType;

		WarningState = report.WarningState;
		ReportTime = report.ReportTime.LocalDateTime;
		TotalAreaCount = report.Regions.Count(a => a.Region != 0);

		Reports.Add(report);
	}

	public override bool CheckDuplicate(DCReport report) => report is WeatherReport w && Reports.Any(r => w.Content.SequenceEqual(r.Content));
	public override bool TryProcess(DCReport report)
	{
		if (report is not WeatherReport w || w.ReportTime.LocalDateTime != ReportTime || w.WarningState != WarningState)
			return false;

		Reports.Add(w);
		ReportCount++;
		TotalAreaCount += w.Regions.Count(a => a.Region != 0);
		return true;
	}

	public override Control? DetailDisplayControl => null;
}
