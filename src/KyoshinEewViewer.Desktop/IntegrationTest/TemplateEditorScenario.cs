#if INTEGRATION_TEST
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.VisualTree;
using AvaloniaEdit;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Services.Workflows.BuiltinActions;
using KyoshinEewViewer.Views.Components;
using KyoshinEewViewer.Views.SettingPages;
using System.Linq;
using System.Threading.Tasks;
using static KyoshinEewViewer.Desktop.IntegrationTest.UiOperations;
using static KyoshinEewViewer.Desktop.IntegrationTest.WorkflowPageOperations;

namespace KyoshinEewViewer.Desktop.IntegrationTest;

/// <summary>
/// テンプレートエディタの補完と構文エラーの警告を確認する
/// </summary>
internal static class TemplateEditorScenario
{
	public static async Task RunAsync(IClassicDesktopStyleApplicationLifetime desktop, SettingWindowSession settings)
	{
		UiTestDriver.Step("テンプレートエディタを開く");
		var page = await settings.ShowPageAsync<WorkflowPage>(PageTitle);
		await AddWorkflowAsync(page);
		await ChooseTriggerAsync(page, "アプリケーション起動時");
		var actions = await WaitForControlAsync<MultipleActionControl>(page);
		await AddActionAsync(actions, "ログ出力");
		await ClickByContentAsync(await WaitForControlAsync<LogOutputActionControl>(actions), "編集");
		var dialog = await WaitForAsync(() => desktop.Windows.OfType<TemplateEditorDialog>().FirstOrDefault(w => w.IsVisible), "テンプレート編集ダイアログ");
		var editor = dialog.FindControl<TextEditor>("Editor")!;

		UiTestDriver.Step("イベントのプロパティの補完を確認する");
		await TypeAndExpectCompletionAsync(dialog, editor, "{{ ", "E", "EventType");

		UiTestDriver.Step("組み込み関数の補完を確認する");
		await TypeAndExpectCompletionAsync(dialog, editor, "{{ string", ".", "slice");

		UiTestDriver.Step("構文エラーの警告を確認する");
		editor.Text = "{{ if";
		await ClickByContentAsync(dialog, "OK");
		var warning = await WaitForControlAsync<FAContentDialog>(dialog);
		await WaitForTextAsync(warning, "テンプレートシンタックスエラー");
		await ClickByContentAsync(warning, "編集に戻る");
		await WaitUntilAsync(() => !dialog.GetVisualDescendants().OfType<FAContentDialog>().Any(), "警告が閉じる");

		UiTestDriver.Step("編集を取り消して元の状態に戻す");
		await ClickByContentAsync(dialog, "キャンセル");
		await WaitUntilAsync(() => !desktop.Windows.Contains(dialog), "テンプレート編集ダイアログが閉じる");
		await ClickByTipAsync(page, RestoreTip);
		await WaitForTextAsync(page, EmptyMessage);
	}

	/// <summary>
	/// 入力に応じて開いた補完ウィンドウの候補を確認する。文字列の代入では補完が開かないので、入力として渡す
	/// </summary>
	private static async Task TypeAndExpectCompletionAsync(TemplateEditorDialog dialog, TextEditor editor, string initial, string typed, string expected)
	{
		editor.Text = initial;
		editor.CaretOffset = initial.Length;
		editor.TextArea.PerformTextInput(typed);
		await WaitUntilAsync(
			() => dialog.OpenCompletionWindow?.CompletionList.CompletionData.Any(d => d.Text == expected) == true,
			$"補完候補「{expected}」");
		dialog.OpenCompletionWindow!.Close();
		await WaitUntilAsync(() => dialog.OpenCompletionWindow == null, "補完ウィンドウが閉じる");
	}
}
#endif
