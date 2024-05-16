using Avalonia.Controls;

namespace KyoshinEewViewer.Services.Workflows.BuiltinTriggers;

public class ApplicationStartupTrigger : WorkflowTrigger
{
	public override Control DisplayControl => new TextBlock { Text = "アプリケーション起動完了時に一度だけトリガーされます。" };

	public override bool CheckTrigger(WorkflowEvent content)
		=> content is ApplicationStartupEvent;

	public override WorkflowEvent CreateTestEvent()
		=> new ApplicationStartupEvent { IsTest = true };
}
public class ApplicationStartupEvent() : WorkflowEvent("ApplicationStartup");
