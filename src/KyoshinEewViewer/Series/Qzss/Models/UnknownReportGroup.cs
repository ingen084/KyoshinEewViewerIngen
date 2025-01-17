using KyoshinEewViewer.DCReportParser;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Series.Qzss.Models;

public class UnknownReportGroup : DCReportGroup
{
    public List<DCReport> Reports { get; } = new();

    public UnknownReportGroup(DCReport report)
    {
        Reports.Add(report);
    }

    public override bool CheckDuplicate(DCReport report) => Reports.Any(r => report.Content.SequenceEqual(r.Content));
    public override bool TryProcess(DCReport report) => false;
}
