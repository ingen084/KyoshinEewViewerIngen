using KyoshinEewViewer.Series.Qzss.Models;
using KyoshinEewViewer.Services.Workflows;
using System.ComponentModel;

namespace KyoshinEewViewer.Series.Qzss.Workflow;

public class QzssEvent(QzssSeries? series, QzssEventType subType, string sentence, DisasterCrisisInformation information) : WorkflowEvent("Qzss", series)
{
	[Description("QZSS 災危通報のイベント種別 (NewSentenceReceived, ReportGroupCreated, ReportGroupUpdated, NankaiTroughReportCompleted)")]
	public QzssEventType EventSubType { get; init; } = subType;

	[Description("受信した災危通報の生センテンス")]
	public string Sentence { get; init; } = sentence;

	[Description("災危通報の解析結果")]
	public DisasterCrisisInformation Information { get; init; } = information;
}

public enum QzssEventType
{
	NewSentenceReceived,

	ReportGroupCreated,
	ReportGroupUpdated,

	NankaiTroughReportCompleted,
}
