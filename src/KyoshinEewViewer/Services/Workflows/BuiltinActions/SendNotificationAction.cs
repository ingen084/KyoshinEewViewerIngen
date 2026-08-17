using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.Notification;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using KyoshinEewViewer.Core;

namespace KyoshinEewViewer.Services.Workflows.BuiltinActions;

public class SendNotificationAction : WorkflowAction
{
	[JsonIgnore]
	public override Control DisplayControl => new SendNotificationActionControl() { DataContext = this };

	private string _title = "アクションによる通知タイトル";
	public string Title
	{
		get => _title;
		set => SetProperty(ref _title, value);
	}

	private string _templateText = "アクションによる通知本文";
	public string TemplateText
	{
		get => _templateText;
		set => SetProperty(ref _templateText, value);
	}

	private string _urgency = "normal";
	/// <summary>
	/// 緊急度。Scriban テンプレートとして評価し low / normal / critical のいずれかに解決する
	/// (ビルトインのワークフローでは内容に応じた条件式を設定する)
	/// </summary>
	public string Urgency
	{
		get => _urgency;
		set => SetProperty(ref _urgency, value);
	}

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
