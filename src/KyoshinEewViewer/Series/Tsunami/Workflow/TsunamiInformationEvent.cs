using KyoshinEewViewer.Series.Tsunami.Models;
using KyoshinEewViewer.Services.Workflows;
using System.ComponentModel;

namespace KyoshinEewViewer.Series.Tsunami.Workflow;

public class TsunamiInformationEvent(TsunamiSeries? series) : WorkflowEvent("TsunamiInformation", series)
{
	[Description("津波情報の本体 (地域別の警報・注意報詳細を含む)")]
	public required TsunamiInfo? TsunamiInfo { get; init; }

	[Description("現在の津波警報レベル (None, Forecast, Advisory, Warning, MajorWarning)")]
	public TsunamiLevel Level => TsunamiInfo?.Level ?? TsunamiLevel.None;

	[Description("更新前の津波警報レベル (None, Forecast, Advisory, Warning, MajorWarning) - 前回情報からの変化検出用")]
	public required TsunamiLevel PreviousLevel { get; init; }
}
