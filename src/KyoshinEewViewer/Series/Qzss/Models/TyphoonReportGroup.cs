using Avalonia.Controls;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Jma;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Series.Qzss.Models;

public class TyphoonReportGroup : DCReportGroup
{
    public List<TyphoonReport> Reports { get; } = [];

    private DateTime _reportTime;
    public DateTime ReportTime
    {
        get => _reportTime;
        set => this.RaiseAndSetIfChanged(ref _reportTime, value);
    }

    private byte _typhoonNumber;
    public byte TyphoonNumber
    {
        get => _typhoonNumber;
        set => this.RaiseAndSetIfChanged(ref _typhoonNumber, value);
    }

    public TyphoonReportGroup(TyphoonReport report)
    {
        Classification = report.ReportClassification;
        InformationType = report.InformationType;

        ReportTime = report.ReportTime.LocalDateTime;
        TyphoonNumber = report.TyphoonNumber;

        Reports.Add(report);
    }

    public override bool CheckDuplicate(DCReport report) => report is TyphoonReport n && Reports.Any(r => n.Content.SequenceEqual(r.Content));
    public override bool TryProcess(DCReport report) => false;

	public override Control? DetailDisplayControl => null;
}
