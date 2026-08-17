using CommunityToolkit.Mvvm.Messaging;
using KyoshinEewViewer.DCReportParser;

namespace KyoshinEewViewer.Series.Qzss.Events;

public class ProcessManualDCReportRequested(DCReport report)
{
	public DCReport Report { get; } = report;

	public static void Request(DCReport report)
		=> StrongReferenceMessenger.Default.Send(new ProcessManualDCReportRequested(report));
}
