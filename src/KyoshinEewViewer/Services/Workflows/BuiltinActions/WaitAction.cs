using Avalonia.Controls;
using ReactiveUI;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services.Workflows.BuiltinActions;
public class WaitAction: WorkflowAction
{
	public override Control DisplayControl => new WaitActionControl() { DataContext = this };

	private int _waitTime = 0;
	public int WaitTime
	{
		get => _waitTime;
		set => this.RaiseAndSetIfChanged(ref _waitTime, value);
	}

	public override Task ExecuteAsync(WorkflowEvent content)
		=> Task.Delay(WaitTime);
}
