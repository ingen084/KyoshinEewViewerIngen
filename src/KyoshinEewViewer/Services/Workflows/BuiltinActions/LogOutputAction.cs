using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.Core;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace KyoshinEewViewer.Services.Workflows.BuiltinActions;

public partial class LogOutputAction : WorkflowAction
{
	[JsonIgnore]
	public override Control DisplayControl => new LogOutputActionControl() { DataContext = this };

	[ObservableProperty]
	public partial string TemplateText { get; set; } = "アクションによるログ出力";

	[JsonIgnore]
	[ObservableProperty]
	public partial string LatestOutput { get; set; } = "";

	public async override Task ExecuteAsync(WorkflowEvent content)
	{
		var template = Scriban.Template.Parse(TemplateText);
		var message = (await template.RenderAsync(content, m => m.Name)).Trim();
		LatestOutput = message;
		AppLog.Create<LogOutputAction>().LogInformation("{Message}", message);
	}
}
