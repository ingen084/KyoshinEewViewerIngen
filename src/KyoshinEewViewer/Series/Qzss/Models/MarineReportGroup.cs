using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Jma;
using ReactiveUI;
using System;
using System.Collections.Generic;
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

    public MarineReportGroup(MarineReport report)
    {
        Classification = report.ReportClassification;
        InformationType = report.InformationType;

        ReportTime = report.ReportTime.LocalDateTime;
        TotalAreaCount = report.Regions.Count(a => a.Region != 0);

        Reports.Add(report);
    }

    public override bool CheckDuplicate(DCReport report) => report is MarineReport m && Reports.Any(r => m.Content.SequenceEqual(r.Content));
    public override bool TryProcess(DCReport report)
    {
        if (report is not MarineReport m || m.ReportTime.LocalDateTime != ReportTime)
            return false;

        Reports.Add(m);
        ReportCount++;
        TotalAreaCount += m.Regions.Count(a => a.Region != 0);
        return true;
    }
}
