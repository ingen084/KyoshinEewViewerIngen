using Avalonia.Controls;
using Avalonia.Interactivity;
using KyoshinEewViewer.Views.Components;

namespace KyoshinEewViewer.Services.Workflows.BuiltinActions;
public partial class SendNotificationActionControl : UserControl
{
	public SendNotificationActionControl()
	{
		InitializeComponent();
	}

	private async void EditTitleButton_Click(object? sender, RoutedEventArgs e)
	{
		if (DataContext is not SendNotificationAction action)
			return;

		var (success, templateText) = await TemplateEditorDialog.ShowAsync(
			"通知タイトルテンプレート編集",
			action.Title,
			action.FindEventType());

		if (success && templateText != null)
			action.Title = templateText;
	}

	private async void EditTemplateButton_Click(object? sender, RoutedEventArgs e)
	{
		if (DataContext is not SendNotificationAction action)
			return;

		var (success, templateText) = await TemplateEditorDialog.ShowAsync(
			"通知本文テンプレート編集",
			action.TemplateText,
			action.FindEventType());

		if (success && templateText != null)
			action.TemplateText = templateText;
	}
}
