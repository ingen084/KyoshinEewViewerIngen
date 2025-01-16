using KyoshinEewViewer.DCReportParser.Jma;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Series.Qzss.Models;

public class TsunamiReportGroup : DCReportGroup
{
    public List<TsunamiReport> Reports { get; } = [];

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

    private byte _warningCode;
    public byte WarningCode
    {
        get => _warningCode;
        set => this.RaiseAndSetIfChanged(ref _warningCode, value);
    }

    public TsunamiReportGroup(TsunamiReport report)
    {
        Classification = report.ReportClassification;
        InformationType = report.InformationType;

        ReportTime = report.ReportTime.LocalDateTime;
        TotalAreaCount = report.Regions.Count(a => a.Region != 0);
        WarningCode = report.WarningCode;

        Reports.Add(report);
    }

    public override bool CheckDuplicate(DCReport report) => report is TsunamiReport t && Reports.Any(r => t.Content.SequenceEqual(r.Content));
    public override bool TryProcess(DCReport report)
    {
        if (report is not TsunamiReport t || t.ReportTime.LocalDateTime != ReportTime)
            return false;

        if (t.WarningCode > WarningCode)
            WarningCode = t.WarningCode;
        Reports.Add(t);
        ReportCount++;
        TotalAreaCount += t.Regions.Count(a => a.Region != 0);
        return true;
    }
}
