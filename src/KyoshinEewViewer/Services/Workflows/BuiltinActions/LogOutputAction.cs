using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.Core;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace KyoshinEewViewer.Services.Workflows.BuiltinActions;

public class LogOutputAction : WorkflowAction
{
	[JsonIgnore]
	public override Control DisplayControl => new LogOutputActionControl() { DataContext = this };

	private string _templateText = "アクションによるログ出力";
	public string TemplateText
	{
		get => _templateText;
		set => SetProperty(ref _templateText, value);
	}

	private string _latestOutput = "";
	[JsonIgnore]
	public string LatestOutput
	{
		get => _latestOutput;
		set => SetProperty(ref _latestOutput, value);
	}

	public async override Task ExecuteAsync(WorkflowEvent content)
	{
		var template = Scriban.Template.Parse(TemplateText);
		var message = (await template.RenderAsync(content, m => m.Name)).Trim();
		LatestOutput = message;
		AppLog.Create<LogOutputAction>().LogInformation("{Message}", message);
	}
}
