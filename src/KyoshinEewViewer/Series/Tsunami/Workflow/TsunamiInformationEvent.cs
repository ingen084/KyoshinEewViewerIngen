using KyoshinEewViewer.Series.Tsunami.Models;
using KyoshinEewViewer.Services.Workflows;

namespace KyoshinEewViewer.Series.Tsunami.Workflow;

public class TsunamiInformationEvent() : WorkflowEvent("TsunamiInformation")
{
	public required TsunamiInfo? TsunamiInfo { get; init; }

	public TsunamiLevel Level => TsunamiInfo?.Level ?? TsunamiLevel.None;
	public required TsunamiLevel PreviousLevel { get; init; }
}
