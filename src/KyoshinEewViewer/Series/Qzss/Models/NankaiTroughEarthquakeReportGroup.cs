using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.DCReportParser.Jma;
using R3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Series.Qzss.Models;

public partial class NankaiTroughEarthquakeReportGroup : DCReportGroup
{
	public static readonly string TYPE = "NankaiTrough";
	public override string Type => TYPE;

	private List<NankaiTroughEarthquakeReport> Reports { get; } = [];

    [ObservableProperty]
    public partial byte TotalPage { get; set; }

    [ObservableProperty]
    public partial byte CurrentProgress { get; set; }

    [ObservableProperty]
    public partial string? CurrentProgressString { get; private set; }

    [ObservableProperty]
    public partial InformationSerialCode InformationSerialCode { get; set; }

    [ObservableProperty]
    public partial string? Contents { get; set; }

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
