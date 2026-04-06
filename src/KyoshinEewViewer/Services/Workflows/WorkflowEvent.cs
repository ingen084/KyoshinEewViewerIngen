using KyoshinEewViewer.Series;
using KyoshinEewViewer.Series.Earthquake.Workflow;
using KyoshinEewViewer.Series.KyoshinMonitor.Workflow;
using KyoshinEewViewer.Series.Qzss.Workflow;
using KyoshinEewViewer.Series.Tsunami.Workflow;
using KyoshinEewViewer.Services.Workflows.BuiltinTriggers;
using System;
using System.ComponentModel;
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
[JsonDerivedType(typeof(TsunamiInformationEvent))]
[JsonDerivedType(typeof(QzssEvent))]
public abstract class WorkflowEvent(string eventType, SeriesBase? eventedSeries)
{
	[JsonIgnore]
	public SeriesBase? EventedSeries { get; } = eventedSeries;

	[Description("イベントの種類 (ShakeDetected / Eew / EarthquakeInformation など)")]
	public string EventType { get; } = eventType;

	[Description("イベントの一意 ID")]
	public Guid EventId { get; } = Guid.NewGuid();

	[Description("テスト実行で発火したイベントかどうか")]
	public bool IsTest { get; init; }
}
