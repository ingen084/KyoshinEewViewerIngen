using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Jma;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Series.Qzss.Models;

public record NPTsunamiArea(byte Code, string Status, string Height);
public class NorthwestPacificTsunamiReportGroup : DCReportGroup
{
	public static readonly string TYPE = "NorthwestPacificTsunami";
	public override string Type => TYPE;

	private List<NorthwestPacificTsunamiReport> Reports { get; } = [];

	private int _totalAreaCount;
	public int TotalAreaCount
	{
		get => _totalAreaCount;
		set => SetProperty(ref _totalAreaCount, value);
	}

	private byte _tsunamigenicPotential;
	public byte TsunamigenicPotential
	{
		get => _tsunamigenicPotential;
		set => SetProperty(ref _tsunamigenicPotential, value);
	}

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
		foreach (var report in Reports)
		{
			foreach (var area in report.Regions)
			{
				if (area.Region == 0)
					continue;
				Areas.Add(new(area.Region, area.IsArrived ? "到達" : ApplyTimezoneOffset(area.ArrivalTime).ToString("HH:mm 到達見込み"), GetTsunamiHeightString(area.Height)));
			}
		}
	}
}
