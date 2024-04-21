using Avalonia.Controls;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Services.Workflows;

public record WorkflowTriggerInfo(Type Type, string DisplayName, Func<WorkflowTrigger> Create);

[JsonDerivedType(typeof(DummyTrigger), typeDiscriminator: "Dummy")]
public abstract class WorkflowTrigger : ReactiveObject
{
	static WorkflowTrigger()
	{
		WorkflowService.RegisterTrigger<DummyTrigger>("何もしない");
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

    public class TestEvent : WorkflowEvent
	{
		public TestEvent(): base("Test", new Dictionary<string, string>())
		{
			IsTest = true;
		}
		public DateTime Time { get; } = DateTime.Now;
	}
}
