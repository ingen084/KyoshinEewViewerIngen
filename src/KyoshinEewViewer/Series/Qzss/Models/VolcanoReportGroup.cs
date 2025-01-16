using KyoshinEewViewer.DCReportParser.Jma;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Series.Qzss.Models;

public class VolcanoReportGroup : DCReportGroup
{
    public List<VolcanoReport> Reports { get; } = [];

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

    private byte _warningCode;
    public byte WarningCode
    {
        get => _warningCode;
        set => this.RaiseAndSetIfChanged(ref _warningCode, value);
    }

    public VolcanoReportGroup(VolcanoReport report)
    {
        Classification = report.ReportClassification;
        InformationType = report.InformationType;

        ReportTime = report.ReportTime.LocalDateTime;
        TotalAreaCount = report.Regions.Count(a => a != 0);
        VolcanoNameCode = report.VolcanoNameCode;
        WarningCode = report.WarningCode;

        Reports.Add(report);
    }

    public override bool CheckDuplicate(DCReport report) => report is VolcanoReport v && Reports.Any(r => v.Content.SequenceEqual(r.Content));
    public override bool TryProcess(DCReport report)
    {
        if (report is not VolcanoReport v || v.ReportTime.LocalDateTime != ReportTime || v.VolcanoNameCode != VolcanoNameCode || v.WarningCode != WarningCode)
            return false;

        Reports.Add(v);
        ReportCount++;
        TotalAreaCount += v.Regions.Count(a => a != 0);
        return true;
    }
}
