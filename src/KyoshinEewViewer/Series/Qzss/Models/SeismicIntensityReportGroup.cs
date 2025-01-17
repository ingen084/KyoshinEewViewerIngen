using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Jma;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Series.Qzss.Models;

public class SeismicIntensityReportGroup : DCReportGroup
{
    public List<SeismicIntensityReport> Reports { get; } = [];

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

    private SeismicIntensity _maxIntensity;
    public SeismicIntensity MaxIntensity
    {
        get => _maxIntensity;
        set => this.RaiseAndSetIfChanged(ref _maxIntensity, value);
    }

    public SeismicIntensityReportGroup(SeismicIntensityReport report)
    {
        Classification = report.ReportClassification;
        InformationType = report.InformationType;

        ReportTime = report.ReportTime.LocalDateTime;
        TotalAreaCount = report.Regions.Count(a => a.Region != 0);
        MaxIntensity = report.Regions.Max(r => r.Intensity);

        Reports.Add(report);
    }

    public override bool CheckDuplicate(DCReport report) => report is SeismicIntensityReport si && Reports.Any(r => si.Content.SequenceEqual(r.Content));
    public override bool TryProcess(DCReport report)
    {
        if (report is not SeismicIntensityReport si || si.ReportTime.LocalDateTime != ReportTime)
            return false;

        Reports.Add(si);
        ReportCount++;
        TotalAreaCount += si.Regions.Count(a => a.Region != 0);
        var max = si.Regions.Max(r => r.Intensity);
        if (max > MaxIntensity)
            MaxIntensity = max;
        return true;
    }
}
