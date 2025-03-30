using Avalonia.Controls;
using KyoshinEewViewer.DCReportParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Series.Qzss.Models;

public class UnknownReportGroup : DCReportGroup
{
	public static readonly string TYPE = "Unknown";
	public override string Type => TYPE;

	private List<DCReport> Reports { get; } = new();

    public UnknownReportGroup(DCReport report)
    {
        Reports.Add(report);
    }

    public override bool CheckDuplicate(DCReport report) => Reports.Any(r => report.Content.SequenceEqual(r.Content));
    public override bool TryProcess(DCReport report) => false;

	[JsonIgnore]
	public override Control? DetailDisplayControl => null;
}
