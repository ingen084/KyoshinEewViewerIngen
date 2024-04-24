using Avalonia.Controls;
using KyoshinEewViewer.Series.KyoshinMonitor.Workflow;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Services.Workflows;

public record WorkflowTriggerInfo(Type Type, string DisplayName, Func<WorkflowTrigger> Create);

[JsonDerivedType(typeof(DummyTrigger), typeDiscriminator: "Dummy")]
[JsonDerivedType(typeof(ShakeDetectTrigger), typeDiscriminator: "ShakeDetected")]
public abstract class WorkflowTrigger : ReactiveObject
{
	static WorkflowTrigger()
	{
		WorkflowService.RegisterTrigger<DummyTrigger>("何もしない");
		WorkflowService.RegisterTrigger<ShakeDetectTrigger>("(強震モニタ)揺れ検知");
	}

	[JsonIgnore]
	public abstract Control DisplayControl { get; }
	public abstract bool CheckTrigger(WorkflowEvent content);

	public abstract WorkflowEvent CreateTestEvent();
}

public class DummyTrigger : WorkflowTrigger
{
	public override Control DisplayControl => new TextBlock { Text = "何もしないトリガーです。\nテスト実行以外で実行されることはありません。" };

	public override bool CheckTrigger(WorkflowEvent content)
		=> content is TestEvent;

	public override WorkflowEvent CreateTestEvent()
		=> new TestEvent();
}

public class TestEvent : WorkflowEvent
{
	public TestEvent(): base("Test")
	{
		IsTest = true;
	}
	public DateTime Time { get; } = DateTime.Now;
}
