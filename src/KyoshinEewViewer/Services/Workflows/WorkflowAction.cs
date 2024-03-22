using Avalonia.Controls;
using ReactiveUI;
using System;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services.Workflows;

public record WorkflowActionInfo(Type Type, string DisplayName, Func<WorkflowAction> Create);
public abstract class WorkflowAction : ReactiveObject
{
	public abstract Control DisplayControl { get; }
	public abstract Task ExecuteAsync(string content);
}
