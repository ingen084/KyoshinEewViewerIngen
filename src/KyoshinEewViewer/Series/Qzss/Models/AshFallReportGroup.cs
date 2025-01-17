using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Jma;
using ReactiveUI;
using System;
using System.Collections.Generic;
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

    public AshFallReportGroup(AshFallReport report)
    {
        Classification = report.ReportClassification;
        InformationType = report.InformationType;

        ReportTime = report.ReportTime.LocalDateTime;
        TotalAreaCount = report.Regions.Count(a => a.Region != 0);
        VolcanoNameCode = report.VolcanoNameCode;

        Reports.Add(report);
    }

    public override bool CheckDuplicate(DCReport report) => report is AshFallReport a && Reports.Any(r => a.Content.SequenceEqual(r.Content));
    public override bool TryProcess(DCReport report)
    {
        if (report is not AshFallReport a || a.ReportTime.LocalDateTime != ReportTime || a.VolcanoNameCode != VolcanoNameCode)
            return false;

        Reports.Add(a);
        ReportCount++;
        TotalAreaCount += a.Regions.Count(a => a.Region != 0);
        return true;
    }
}
