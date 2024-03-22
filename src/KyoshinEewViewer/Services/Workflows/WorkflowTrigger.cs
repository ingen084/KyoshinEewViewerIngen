using Avalonia.Controls;
using ReactiveUI;
using System;

namespace KyoshinEewViewer.Services.Workflows;

public record WorkflowTriggerInfo(Type Type, string DisplayName, Func<WorkflowTrigger> Create);
public abstract class WorkflowTrigger : ReactiveObject
{
	public abstract Control DisplayControl { get; }
	public abstract bool CheckTrigger(string content);
}
