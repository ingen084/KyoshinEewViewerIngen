using System.Collections.Generic;

namespace KyoshinEewViewer.Services.Workflows;

/// <summary>
/// ワークフロー上におけるイベント
/// </summary>
public abstract class WorkflowEvent(string eventType, IReadOnlyDictionary<string, string> variables)
{
	public string EventType { get; } = eventType;
	public bool IsTest { get; init; }
	public IReadOnlyDictionary<string, string> Variables { get; } = variables;
}
