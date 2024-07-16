using Avalonia.Controls;
using KyoshinEewViewer.Series.Earthquake.Workflow;
using KyoshinEewViewer.Series.KyoshinMonitor.Workflow;
using KyoshinEewViewer.Series.Tsunami.Workflow;
using KyoshinEewViewer.Services.Workflows.BuiltinTriggers;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Services.Workflows;

public record WorkflowTriggerInfo(Type Type, string DisplayName, Func<WorkflowTrigger> Create);

[JsonDerivedType(typeof(DummyTrigger), typeDiscriminator: "Dummy")]
[JsonDerivedType(typeof(ShakeDetectTrigger), typeDiscriminator: "KyoshinShakeDetected")]
[JsonDerivedType(typeof(EewTrigger), typeDiscriminator: "Eew")]
[JsonDerivedType(typeof(ApplicationStartupTrigger), typeDiscriminator: "ApplicationStartup")]
[JsonDerivedType(typeof(UpdateAvailableTrigger), typeDiscriminator: "UpdateAvailable")]
[JsonDerivedType(typeof(EarthquakeInformationTrigger), typeDiscriminator: "EarthquakeInformation")]
[JsonDerivedType(typeof(TsunamiInformationTrigger), typeDiscriminator: "TsunamiInformation")]
public abstract class WorkflowTrigger : ReactiveObject
{
	static WorkflowTrigger()
	{
		WorkflowService.RegisterTrigger<DummyTrigger>("何もしない");
		WorkflowService.RegisterTrigger<ApplicationStartupTrigger>("アプリケーション起動時");
		WorkflowService.RegisterTrigger<UpdateAvailableTrigger>("アプリケーションの更新存在時");
		WorkflowService.RegisterTrigger<ShakeDetectTrigger>("(強震モニタ)揺れ検知");
		WorkflowService.RegisterTrigger<EewTrigger>("(強震モニタ)緊急地震速報");
		WorkflowService.RegisterTrigger<EarthquakeInformationTrigger>("(地震情報)地震情報受信");
		WorkflowService.RegisterTrigger<TsunamiInformationTrigger>("(津波情報)津波情報更新時");
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
