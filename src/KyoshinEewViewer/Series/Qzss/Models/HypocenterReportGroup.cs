using KyoshinEewViewer.DCReportParser.Jma;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Series.Qzss.Models;

public class HypocenterReportGroup : DCReportGroup
{
    public List<HypocenterReport> Reports { get; } = [];

    private DateTime _reportTime;
    public DateTime ReportTime
    {
        get => _reportTime;
        set => this.RaiseAndSetIfChanged(ref _reportTime, value);
    }

    private DateTime _occurrenceTime;
    public DateTime OccurrenceTime
    {
        get => _occurrenceTime;
        set => this.RaiseAndSetIfChanged(ref _occurrenceTime, value);
    }

    private byte _rawMagnitude;
    public byte RawMagnitude
    {
        get => _rawMagnitude;
        set => this.RaiseAndSetIfChanged(ref _rawMagnitude, value);
    }

    private int _epicenter;
    public int Epicenter
    {
        get => _epicenter;
        set => this.RaiseAndSetIfChanged(ref _epicenter, value);
    }

    public HypocenterReportGroup(HypocenterReport report)
    {
        Classification = report.ReportClassification;
        InformationType = report.InformationType;

        ReportTime = report.ReportTime.LocalDateTime;
        OccurrenceTime = report.OccurrenceTime.LocalDateTime;
        RawMagnitude = report.Magnitude;
        Epicenter = report.Epicenter;

        Reports.Add(report);
    }

    public override bool CheckDuplicate(DCReport report) => report is HypocenterReport h && Reports.Any(r => h.Content.SequenceEqual(r.Content));
    public override bool TryProcess(DCReport report) => false;
}
