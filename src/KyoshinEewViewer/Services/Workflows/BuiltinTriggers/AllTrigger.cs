using Avalonia.Controls;

namespace KyoshinEewViewer.Services.Workflows.BuiltinTriggers;

public class AllTrigger : WorkflowTrigger
{
	public override Control DisplayControl => new TextBlock { Text = "すべてのイベントに対しトリガーされます。" };

	public override bool CheckTrigger(WorkflowEvent content) => true;
	public override WorkflowEvent CreateTestEvent() => new TestEvent();
}
