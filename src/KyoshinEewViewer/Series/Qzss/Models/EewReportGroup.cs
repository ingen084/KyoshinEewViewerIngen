using KyoshinEewViewer.DCReportParser.Jma;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Series.Qzss.Models;

public class EewReportGroup : DCReportGroup
{
    public List<EewReport> Reports { get; } = [];

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

    private int _totalAreaCount;
    public int TotalAreaCount
    {
        get => _totalAreaCount;
        set => this.RaiseAndSetIfChanged(ref _totalAreaCount, value);
    }

    private EewSeismicIntensity _intensity;
    public EewSeismicIntensity Intensity
    {
        get => _intensity;
        set => this.RaiseAndSetIfChanged(ref _intensity, value);
    }

    private bool _isIntensityOver;
    public bool IsIntensityOver
    {
        get => _isIntensityOver;
        set => this.RaiseAndSetIfChanged(ref _isIntensityOver, value);
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

    public EewReportGroup(EewReport report)
    {
        Classification = report.ReportClassification;
        InformationType = report.InformationType;

        ReportTime = report.ReportTime.LocalDateTime;
        OccurrenceTime = report.OccurrenceTime.LocalDateTime;
        // index 56 以降はまとめられた地域のため無視する
        for (var i = 0; i < 56; i++)
            if (report.WarningRegions[i])
                TotalAreaCount++;
        Intensity = report.SeismicIntensityLowerLimit;
        IsIntensityOver = report.SeismicIntensityUpperLimit == EewSeismicIntensity.Over;
        RawMagnitude = report.Magnitude;
        Epicenter = report.Epicenter;

        Reports.Add(report);
    }

    public override bool CheckDuplicate(DCReport report) => report is EewReport eew && Reports.Any(r => eew.Content.SequenceEqual(r.Content));
    public override bool TryProcess(DCReport report) => false;
}
