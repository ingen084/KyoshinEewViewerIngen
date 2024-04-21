using Avalonia.Controls;
using ReactiveUI;
using Splat;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services.Workflows.BuiltinActions;

public class SendNotificationAction: WorkflowAction
{
	public override Control DisplayControl => new SendNotificationActionControl() { DataContext = this };

	private string _title = "通知タイトル";
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
		=> Locator.Current.GetService<NotificationService>()?.Notify(
			Title,
			await Scriban.Template.Parse(TemplateText).RenderAsync(content, m => m.Name)
		);
}
