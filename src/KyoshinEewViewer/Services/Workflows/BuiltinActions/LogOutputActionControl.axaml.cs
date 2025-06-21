using Avalonia.Controls;
using Avalonia.Interactivity;
using KyoshinEewViewer.Views.Components;

namespace KyoshinEewViewer.Services.Workflows.BuiltinActions;
public partial class LogOutputActionControl : UserControl
{
	public LogOutputActionControl()
	{
		InitializeComponent();
	}
	
	private async void EditTemplateButton_Click(object? sender, RoutedEventArgs e)
	{
		if (DataContext is not LogOutputAction action)
			return;

		var (success, templateText) = await TemplateEditorDialog.ShowAsync(
			"ログ出力テンプレート編集", 
			action.TemplateText);

		if (success && templateText != null)
			action.TemplateText = templateText;
	}
}
