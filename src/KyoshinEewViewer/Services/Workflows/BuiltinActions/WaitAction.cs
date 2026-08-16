using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services.Workflows.BuiltinActions;
public class WaitAction: WorkflowAction
{
	[JsonIgnore]
	public override Control DisplayControl => new WaitActionControl() { DataContext = this };

	private int _waitTime = 0;
	public int WaitTime
	{
		get => _waitTime;
		set => SetProperty(ref _waitTime, value);
	}

	public override Task ExecuteAsync(WorkflowEvent content)
		=> Task.Delay(WaitTime);
}
