using Avalonia.Controls;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Jma;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Series.Qzss.Models;

public record TyphoonInformation(DateTime Time, ReferenceTimeType TimeType, byte ElapsedHours, byte Scale, byte Intensity, int CentralPressure, byte MaxWindSpeed, byte MaxWindGustSpeed, KyoshinMonitorLib.Location CenterLocation);
public class TyphoonReportGroup : DCReportGroup
{
	public static readonly string TYPE = "Typhoon";
	public override string Type => TYPE;

	private List<TyphoonReport> Reports { get; } = [];

	private byte _typhoonNumber;
	public byte TyphoonNumber
	{
		get => _typhoonNumber;
		set => this.RaiseAndSetIfChanged(ref _typhoonNumber, value);
	}

	private TyphoonInformation[] _typhoonInformations = [];
	public TyphoonInformation[] TyphoonInformations
	{
		get => _typhoonInformations;
		set => this.RaiseAndSetIfChanged(ref _typhoonInformations, value);
	}

	public TyphoonReportGroup(TyphoonReport report)
	{
		Classification = report.ReportClassification;
		InformationType = report.InformationType;

		ReportTime = ApplyTimezoneOffset(report.ReportTime);
		TyphoonNumber = report.TyphoonNumber;

		Reports.Add(report);
		ProcessInformation();

		MapDisplayParameter = new()
		{
			Padding = new(205, 0, 0, 0),
		};
	}

	public override bool CheckDuplicate(DCReport report) => report is TyphoonReport n && Reports.Any(r => n.Content.SequenceEqual(r.Content));
	public override bool TryProcess(DCReport report)
	{
		if (report is not TyphoonReport t || ApplyTimezoneOffset(t.ReportTime) != ReportTime || t.TyphoonNumber != TyphoonNumber)
			return false;

		Reports.Add(t);
		ReportCount++;
		ProcessInformation();
		return true;
	}

	[JsonIgnore]
	public override Control? DetailDisplayControl => new TyphoonReportControl { DataContext = this };

	public void ProcessInformation()
	{
		var infos = new List<TyphoonInformation>();
		foreach (var report in Reports)
		{
			infos.Add(new(
				ApplyTimezoneOffset(report.ReferenceTime),
				report.ReferenceTimeType,
				report.ElapsedTime,
				report.ScaleCategory,
				report.IntensityCategory,
				report.CentralPressure,
				report.MaximumWindSpeed,
				report.MaximumWindGustSpeed,
				new(report.Latitude, report.Longitude)
			));
		}
		TyphoonInformations = infos.OrderBy(i => i.ElapsedHours).ToArray();
	}
}
