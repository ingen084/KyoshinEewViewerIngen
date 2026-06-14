using System;
using Avalonia.Controls;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Services.Workflows.BuiltinTriggers;

public class AllTrigger : WorkflowTrigger
{
	public override Type EventType => typeof(WorkflowEvent);
	[JsonIgnore]
	public override Control DisplayControl => new TextBlock { Text = "すべてのイベントに対しトリガーされます。" };

	public override bool CheckTrigger(WorkflowEvent content) => true;
	public override WorkflowEvent CreateTestEvent() => new TestEvent();
}
