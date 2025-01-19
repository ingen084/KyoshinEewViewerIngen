using Avalonia.Controls;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Jma;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Series.Qzss.Models;

public class NorthwestPacificTsunamiReportGroup : DCReportGroup
{
    public List<NorthwestPacificTsunamiReport> Reports { get; } = [];

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

    private byte _tsunamigenicPotential;
    public byte TsunamigenicPotential
    {
        get => _tsunamigenicPotential;
        set => this.RaiseAndSetIfChanged(ref _tsunamigenicPotential, value);
    }

    public NorthwestPacificTsunamiReportGroup(NorthwestPacificTsunamiReport report)
    {
        Classification = report.ReportClassification;
        InformationType = report.InformationType;

        ReportTime = report.ReportTime.LocalDateTime;
        TotalAreaCount = report.Regions.Count(a => a.Region != 0);
        TsunamigenicPotential = report.TsunamigenicPotential;
    }

    public override bool CheckDuplicate(DCReport report) => report is NorthwestPacificTsunamiReport n && Reports.Any(r => n.Content.SequenceEqual(r.Content));
    public override bool TryProcess(DCReport report)
    {
        if (report is not NorthwestPacificTsunamiReport n || n.ReportTime.LocalDateTime != ReportTime || n.TsunamigenicPotential != TsunamigenicPotential)
            return false;

        Reports.Add(n);
        ReportCount++;
        TotalAreaCount += n.Regions.Count(a => a.Region != 0);
        return true;
	}

	public override Control? DetailDisplayControl => null;
}
