using Avalonia.Controls;
using KyoshinEewViewer.DCReportParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Series.Qzss.Models;

public class DCXReportGroup : DCReportGroup
{
	public static readonly string TYPE = "DCX";
	public override string Type => TYPE;

	private List<OtherOrganizationDCReport> Reports { get; } = [];

    public DCXReportGroup(OtherOrganizationDCReport report)
    {
        Classification = report.ReportClassification;

        Reports.Add(report);
    }

    public override bool CheckDuplicate(DCReport report) => Reports.Any(r => report.Content.SequenceEqual(r.Content));
    public override bool TryProcess(DCReport report) => false;

	[JsonIgnore]
	public override Control? DetailDisplayControl => null;
}
