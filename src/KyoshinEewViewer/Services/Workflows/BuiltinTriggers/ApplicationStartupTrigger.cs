using System;
using Avalonia.Controls;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Services.Workflows.BuiltinTriggers;

public class ApplicationStartupTrigger : WorkflowTrigger
{
	public override Type EventType => typeof(ApplicationStartupEvent);
	[JsonIgnore]
	public override Control DisplayControl => new TextBlock { Text = "アプリケーション起動完了時に一度だけトリガーされます。" };

	public override bool CheckTrigger(WorkflowEvent content)
		=> content is ApplicationStartupEvent;

	public override WorkflowEvent CreateTestEvent()
		=> new ApplicationStartupEvent { IsTest = true };
}
public class ApplicationStartupEvent() : WorkflowEvent("ApplicationStartup", null);
