using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.Notification;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using KyoshinEewViewer.Core;

namespace KyoshinEewViewer.Services.Workflows.BuiltinActions;

public partial class SendNotificationAction : WorkflowAction
{
	[JsonIgnore]
	public override Control DisplayControl => new SendNotificationActionControl() { DataContext = this };

	[ObservableProperty]
	public partial string Title { get; set; } = "アクションによる通知タイトル";

	[ObservableProperty]
	public partial string TemplateText { get; set; } = "アクションによる通知本文";

	/// <summary>
	/// 緊急度。Scriban テンプレートとして評価し low / normal / critical のいずれかに解決する
	/// (ビルトインのワークフローでは内容に応じた条件式を設定する)
	/// </summary>
	[ObservableProperty]
	public partial string Urgency { get; set; } = "normal";

	public async override Task ExecuteAsync(WorkflowEvent content)
	{
		var title = await Scriban.Template.Parse(Title).RenderAsync(content, m => m.Name);
		var message = await Scriban.Template.Parse(TemplateText).RenderAsync(content, m => m.Name);
		if (string.IsNullOrWhiteSpace(message) || string.IsNullOrWhiteSpace(title))
			return;

		var urgencyText = (await Scriban.Template.Parse(Urgency).RenderAsync(content, m => m.Name)).Trim();

		ServiceLocator.Current.GetService<NotificationService>()?.Notify(
			new NotificationRequest(title, message, ParseUrgency(urgencyText))
		);
	}

	private static NotificationUrgency ParseUrgency(string value)
		=> value.ToLowerInvariant() switch
		{
			"low" => NotificationUrgency.Low,
			"critical" => NotificationUrgency.Critical,
			_ => NotificationUrgency.Normal,
		};
}
