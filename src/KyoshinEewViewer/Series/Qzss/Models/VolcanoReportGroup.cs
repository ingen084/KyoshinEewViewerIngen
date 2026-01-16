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

public class VolcanoReportGroup : DCReportGroup
{
	public static readonly string TYPE = "Volcano";
	public override string Type => TYPE;

	private List<VolcanoReport> Reports { get; } = [];

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

	private byte _warningCode;
	public byte WarningCode
	{
		get => _warningCode;
		set => this.RaiseAndSetIfChanged(ref _warningCode, value);
	}

	private byte _ambiguity;
	public byte Ambiguity
	{
		get => _ambiguity;
		set => this.RaiseAndSetIfChanged(ref _ambiguity, value);
	}

	private DateTime _activityTime;
	public DateTime ActivityTime
	{
		get => _activityTime;
		set => this.RaiseAndSetIfChanged(ref _activityTime, value);
	}
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
