using Avalonia.Controls;
using Avalonia.Interactivity;
using KyoshinEewViewer.Views.Components;

namespace KyoshinEewViewer.Services.Workflows.BuiltinActions;
public partial class ExecuteFileActionControl : UserControl
{
	public ExecuteFileActionControl()
	{
		InitializeComponent();
	}

	private async void EditFilePathTemplateButton_Click(object? sender, RoutedEventArgs e)
	{
		if (DataContext is not ExecuteFileAction action)
			return;

		var (success, templateText) = await TemplateEditorDialog.ShowAsync(
			"実行ファイルテンプレート編集",
			action.FilePath,
			action.FindEventType());

		if (success && templateText != null)
			action.FilePath = templateText;
	}

	private async void EditArgumentsTemplateButton_Click(object? sender, RoutedEventArgs e)
	{
		if (DataContext is not ExecuteFileAction action)
			return;

		var (success, templateText) = await TemplateEditorDialog.ShowAsync(
			"起動引数テンプレート編集",
			action.Arguments,
			action.FindEventType());

		if (success && templateText != null)
			action.Arguments = templateText;
	}

	private async void EditWorkingDirectoryTemplateButton_Click(object? sender, RoutedEventArgs e)
	{
		if (DataContext is not ExecuteFileAction action)
			return;

		var (success, templateText) = await TemplateEditorDialog.ShowAsync(
			"作業ディレクトリテンプレート編集",
			action.WorkingDirectory,
			action.FindEventType());

		if (success && templateText != null)
			action.WorkingDirectory = templateText;
	}
}
