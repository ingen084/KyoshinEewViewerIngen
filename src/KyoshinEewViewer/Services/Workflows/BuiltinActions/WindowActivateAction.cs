using Avalonia.Controls;
using KyoshinEewViewer.Core.Models.Events;
using ReactiveUI;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services.Workflows.BuiltinActions;

public class WindowActivateAction : WorkflowAction
{
	public override Control DisplayControl => new TextBlock { Text = "メインウィンドウを最前面に表示します。" };

	public override Task ExecuteAsync(WorkflowEvent content)
	{
		MessageBus.Current.SendMessage(new ShowMainWindowRequested());
		return Task.CompletedTask;
	}
}
