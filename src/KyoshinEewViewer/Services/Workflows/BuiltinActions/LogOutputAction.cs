using Avalonia.Controls;
using KyoshinEewViewer.Core;
using ReactiveUI;
using Splat;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services.Workflows.BuiltinActions;

public class LogOutputAction : WorkflowAction
{
	public override Control DisplayControl => new LogOutputActionControl() { DataContext = this };

	private string _templateText = "アクションによるログ出力";
	public string TemplateText
	{
		get => _templateText;
		set => this.RaiseAndSetIfChanged(ref _templateText, value);
	}

	private string _latestOutput = "";
	[JsonIgnore]
	public string LatestOutput
	{
		get => _latestOutput;
		set => this.RaiseAndSetIfChanged(ref _latestOutput, value);
	}

	public async override Task ExecuteAsync(WorkflowEvent content)
	{
		var template = Scriban.Template.Parse(TemplateText);
		var message = (await template.RenderAsync(content, m => m.Name)).Trim();
		LatestOutput = message;
		Locator.Current.RequireService<ILogManager>().GetLogger<LogOutputAction>().LogInfo(message);
	}
}
