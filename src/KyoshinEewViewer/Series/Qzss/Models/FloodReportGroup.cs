using KyoshinEewViewer.DCReportParser.Jma;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Series.Qzss.Models;

public class FloodReportGroup : DCReportGroup
{
    public List<FloodReport> Reports { get; } = [];

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

    public FloodReportGroup(FloodReport report)
    {
        Classification = report.ReportClassification;
        InformationType = report.InformationType;

        ReportTime = report.ReportTime.LocalDateTime;
        TotalAreaCount = report.Regions.Count(a => a.Region != 0);

        Reports.Add(report);
    }

    public override bool CheckDuplicate(DCReport report) => report is FloodReport f && Reports.Any(r => f.Content.SequenceEqual(r.Content));
    public override bool TryProcess(DCReport report)
    {
        if (report is not FloodReport f || f.ReportTime.LocalDateTime != ReportTime)
            return false;

        Reports.Add(f);
        ReportCount++;
        TotalAreaCount += f.Regions.Count(a => a.Region != 0);
        return true;
    }
}
