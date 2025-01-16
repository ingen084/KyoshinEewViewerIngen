using KyoshinEewViewer.DCReportParser.Jma;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;

namespace KyoshinEewViewer.Series.Qzss.Models;

public class NankaiTroughEarthquakeReportGroup : DCReportGroup
{
    public List<NankaiTroughEarthquakeReport> Reports { get; } = [];

    private DateTime _reportTime;
    public DateTime ReportTime
    {
        get => _reportTime;
        set => this.RaiseAndSetIfChanged(ref _reportTime, value);
    }

    private byte _totalPage;
    public byte TotalPage
    {
        get => _totalPage;
        set => this.RaiseAndSetIfChanged(ref _totalPage, value);
    }

    private byte _currentProgress;
    public byte CurrentProgress
    {
        get => _currentProgress;
        set => this.RaiseAndSetIfChanged(ref _currentProgress, value);
    }

    private readonly ObservableAsPropertyHelper<string?> _currentProgressString;
    public string? CurrentProgressString => _currentProgressString?.Value;

    private InformationSerialCode _informationSerialCode;
    public InformationSerialCode InformationSerialCode
    {
        get => _informationSerialCode;
        set => this.RaiseAndSetIfChanged(ref _informationSerialCode, value);
    }

    private string? _contents;
    public string? Contents
    {
        get => _contents;
        set => this.RaiseAndSetIfChanged(ref _contents, value);
    }

    public NankaiTroughEarthquakeReportGroup(NankaiTroughEarthquakeReport report)
    {
        Classification = report.ReportClassification;
        InformationType = report.InformationType;

        _currentProgressString = this.WhenAnyValue(x => x.CurrentProgress, x => x.TotalPage)
            .Select(x => x.Item1 == x.Item2 ? "受信完了" : $"{x.Item1}/{x.Item2}").ToProperty(this, x => x.CurrentProgressString);

        ReportTime = report.ReportTime.LocalDateTime;
        TotalPage = report.TotalPage;
        InformationSerialCode = report.InformationSerialCode;

        CurrentProgress = 1;
        Reports.Add(report);
        GenerateContents();
    }

    public override bool CheckDuplicate(DCReport report) => report is NankaiTroughEarthquakeReport n && Reports.Any(r => n.Content.SequenceEqual(r.Content));
    public override bool TryProcess(DCReport report)
    {
        if (report is not NankaiTroughEarthquakeReport n)
            return false;

        if (n.InformationSerialCode != InformationSerialCode || n.TotalPage != TotalPage || Reports.Any(r => n.PageNumber == r.PageNumber))
            return false;

        CurrentProgress++;
        Reports.Add(n);
        ReportCount++;
        GenerateContents();
        return true;
    }

    private void GenerateContents()
        => Contents = Encoding.UTF8.GetString(Reports.OrderBy(x => x.PageNumber).SelectMany(x => x.TextInformation).ToArray());
}
