using KyoshinEewViewer.Series.Earthquake.Workflow;
using KyoshinEewViewer.Series.KyoshinMonitor.Workflow;
using KyoshinEewViewer.Services.Workflows.BuiltinTriggers;
using System;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Services.Workflows;

/// <summary>
/// ワークフロー上におけるイベント
/// </summary>
[JsonDerivedType(typeof(TestEvent))]
[JsonDerivedType(typeof(ShakeDetectedEvent))]
[JsonDerivedType(typeof(EewEvent))]
[JsonDerivedType(typeof(ApplicationStartupEvent))]
[JsonDerivedType(typeof(UpdateAvailableEvent))]
[JsonDerivedType(typeof(EarthquakeInformationEvent))]
public abstract class WorkflowEvent(string eventType)
{
	public string EventType { get; } = eventType;
	public Guid EventId { get; } = Guid.NewGuid();
	public bool IsTest { get; init; }
}
