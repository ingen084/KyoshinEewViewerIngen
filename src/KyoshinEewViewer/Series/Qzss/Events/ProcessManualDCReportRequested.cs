using KyoshinEewViewer.DCReportParser;
using ReactiveUI;

namespace KyoshinEewViewer.Series.Qzss.Events;

public class ProcessManualDCReportRequested(DCReport report)
{
	public DCReport Report { get; } = report;

	public static void Request(DCReport report)
		=> MessageBus.Current.SendMessage(new ProcessManualDCReportRequested(report));
}
