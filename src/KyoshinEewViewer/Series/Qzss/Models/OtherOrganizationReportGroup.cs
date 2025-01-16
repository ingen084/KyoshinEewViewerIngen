using KyoshinEewViewer.DCReportParser;
using ReactiveUI;
using System.Collections.Generic;

namespace KyoshinEewViewer.Series.Qzss.Models;

public class OtherOrganizationReportGroup : DCReportGroup
{
    public List<OtherOrganizationDCReport> Reports { get; } = [];

    private string? _organizationName;
    public string? OrganizationName
    {
        get => _organizationName;
        set => this.RaiseAndSetIfChanged(ref _organizationName, value);
    }

    public OtherOrganizationReportGroup(OtherOrganizationDCReport report)
    {
        Classification = report.ReportClassification;

        Reports.Add(report);
        OrganizationName = (report.OrganizationCode switch
        {
            1 => "内閣官房",
            2 => "内閣府(防災)",
            3 => "内閣府(宇宙)",
            4 => "警察庁",
            5 => "金融庁",
            6 => "消費者庁",
            7 => "総務省",
            8 => "消防庁",
            9 => "法務省",
            10 => "公安調査庁",
            11 => "外務省",
            12 => "財務省",
            13 => "国税庁",
            14 => "文部科学省",
            15 => "文化庁",
            16 => "厚生労働省",
            17 => "農林水産省",
            18 => "林野庁",
            19 => "水産庁",
            20 => "経済産業省",
            21 => "資源エネルギー庁",
            22 => "中小企業庁",
            23 => "国土交通省(防災)",
            24 => "国土交通省(危機管理)",
            25 => "国土地理院",
            26 => "観光庁",
            27 => "海上保安庁",
            28 => "環境省",
            29 => "原子力規制委員会",
            30 => "防衛省",
            >= 45 and <= 49 => "予約済み(企業等)",
            51 => "都道府県",
            52 => "市区町村",
            53 => "公的法人",
            60 => "外国",
            _ => "不明"
        }) + $"({report.OrganizationCode})";
    }

    public override bool CheckDuplicate(DCReport report) => Reports.Any(r => report.Content.SequenceEqual(r.Content));
    public override bool TryProcess(DCReport report) => false;
}
