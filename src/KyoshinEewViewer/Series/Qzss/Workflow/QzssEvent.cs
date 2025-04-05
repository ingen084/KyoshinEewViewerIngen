using KyoshinEewViewer.Series.Qzss.Models;
using KyoshinEewViewer.Services.Workflows;

namespace KyoshinEewViewer.Series.Qzss.Workflow;

public class QzssEvent(QzssSeries? series, QzssEventType subType, string sentence, DisasterCrisisInformation information) : WorkflowEvent("Qzss", series)
{
	public QzssEventType EventSubType { get; init; } = subType;
	public string Sentence { get; init; } = sentence;
	public DisasterCrisisInformation Information { get; init; } = information;
}

public enum QzssEventType
{
	NewSentenceReceived,

	ReportGroupCreated,
	ReportGroupUpdated,

	NankaiTroughReportCompleted,
}
