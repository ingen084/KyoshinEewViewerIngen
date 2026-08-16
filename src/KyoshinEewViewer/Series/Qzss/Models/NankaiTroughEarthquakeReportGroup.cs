using Avalonia.Controls;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Jma;
using R3;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Series.Qzss.Models;

public class NankaiTroughEarthquakeReportGroup : DCReportGroup
{
	public static readonly string TYPE = "NankaiTrough";
	public override string Type => TYPE;

	private List<NankaiTroughEarthquakeReport> Reports { get; } = [];

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

    private string? _currentProgressString;
    public string? CurrentProgressString
    {
        get => _currentProgressString;
        private set => this.RaiseAndSetIfChanged(ref _currentProgressString, value);
    }

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

        // 購読元は this 自身のため、this の寿命とともに解放される
        Observable.CombineLatest(
                this.ObservePropertyChanged(x => x.CurrentProgress),
                this.ObservePropertyChanged(x => x.TotalPage),
                (current, total) => current == total ? "受信完了" : $"{current}/{total}")
            .Subscribe(x => CurrentProgressString = x);

        ReportTime = ApplyTimezoneOffset(report.ReportTime);
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

	private static readonly byte[] PLACE_HOLDER = Encoding.UTF8.GetBytes("□□□□□□");
	private void GenerateContents()
	{
		var bytes = new List<byte>();

		for(var i = 0; i < TotalPage; i++)
		{
			if (Reports.FirstOrDefault(x => x.PageNumber == i + 1) is { } report)
				bytes.AddRange(report.TextInformation);
			else
				bytes.AddRange(PLACE_HOLDER);
		}
		Contents = Encoding.UTF8.GetString(bytes.ToArray());
	}

	[JsonIgnore]
	public override Control? DetailDisplayControl => new NankaiTroughEarthquakeReportControl { DataContext = this };
}
