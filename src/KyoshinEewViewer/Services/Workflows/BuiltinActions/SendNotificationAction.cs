using Avalonia.Controls;
using ReactiveUI;
using Splat;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services.Workflows.BuiltinActions;

public class SendNotificationAction : WorkflowAction
{
	[JsonIgnore]
	public override Control DisplayControl => new SendNotificationActionControl() { DataContext = this };

	private string _title = "アクションによる通知タイトル";
	public string Title
	{
		get => _title;
		set => this.RaiseAndSetIfChanged(ref _title, value);
	}

	private string _templateText = "アクションによる通知本文";
	public string TemplateText
	{
		get => _templateText;
		set => this.RaiseAndSetIfChanged(ref _templateText, value);
	}

	public async override Task ExecuteAsync(WorkflowEvent content)
	{
		var title = await Scriban.Template.Parse(Title).RenderAsync(content, m => m.Name);
		var message = await Scriban.Template.Parse(TemplateText).RenderAsync(content, m => m.Name);
		if (string.IsNullOrWhiteSpace(message) || string.IsNullOrWhiteSpace(title))
			return;
		Locator.Current.GetService<NotificationService>()?.Notify(
			title,
			message
		);
	}
}
