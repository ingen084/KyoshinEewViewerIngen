using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services.Workflows.BuiltinActions;
public partial class WaitAction: WorkflowAction
{
	[JsonIgnore]
	public override Control DisplayControl => new WaitActionControl() { DataContext = this };

	[ObservableProperty]
	public partial int WaitTime { get; set; } = 0;

	public override Task ExecuteAsync(WorkflowEvent content)
		=> Task.Delay(WaitTime);
}
