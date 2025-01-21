using Avalonia.Controls;
using KyoshinEewViewer.DCReportParser;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Series.Qzss.Models;

public class DCXReportGroup : DCReportGroup
{
    public List<OtherOrganizationDCReport> Reports { get; } = [];

    public DCXReportGroup(OtherOrganizationDCReport report)
    {
        Classification = report.ReportClassification;

        Reports.Add(report);
    }

    public override bool CheckDuplicate(DCReport report) => Reports.Any(r => report.Content.SequenceEqual(r.Content));
    public override bool TryProcess(DCReport report) => false;

	public override Control? DetailDisplayControl => null;
}
