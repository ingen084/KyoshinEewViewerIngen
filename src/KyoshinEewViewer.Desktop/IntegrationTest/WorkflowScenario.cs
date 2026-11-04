#if INTEGRATION_TEST
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.VisualTree;
using AvaloniaEdit;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Services.Workflows.BuiltinActions;
using KyoshinEewViewer.Views.Components;
using KyoshinEewViewer.Views.SettingPages;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static KyoshinEewViewer.Desktop.IntegrationTest.UiOperations;
using static KyoshinEewViewer.Desktop.IntegrationTest.WorkflowPageOperations;

namespace KyoshinEewViewer.Desktop.IntegrationTest;

/// <summary>
/// ワークフローの作成、テスト実行、保存、読み戻し、削除までを設定画面から行う
/// </summary>
internal static class WorkflowScenario
{
	private const string WorkflowName = "新しいワークフロー";
	private const string TriggerName = "アプリケーション起動時";
	private const string ActionName = "ログ出力";
	private const string TemplateText = "UI-CHECK|{{ EventType }}|{{ \"05\" | string.slice 1 }}";
	private const string ExpectedOutput = "UI-CHECK|ApplicationStartup|5";
	private const string SavedActionType = "\"LogOutput\"";

	public static async Task RunAsync(IClassicDesktopStyleApplicationLifetime desktop, SettingWindowSession settings)
	{
		var workflowsFile = Path.Combine(PlatformDirectories.ApplicationData, "workflows.json");

		UiTestDriver.Step("ワークフロー画面を開く");
		var page = await settings.ShowPageAsync<WorkflowPage>(PageTitle);
		await WaitForTextAsync(page, EmptyMessage);

		UiTestDriver.Step("ワークフローを追加する");
		await AddWorkflowAsync(page);
		await WaitForTextAsync(page, WorkflowName);

		UiTestDriver.Step("トリガーを選ぶ");
		await ChooseTriggerAsync(page, TriggerName);
		await WaitForTextAsync(page, "アプリケーション起動完了時に一度だけトリガーされます。");

		UiTestDriver.Step("アクションを追加して選ぶ");
		var actions = await WaitForControlAsync<MultipleActionControl>(page);
		await AddActionAsync(actions, ActionName);
		var logOutput = await WaitForControlAsync<LogOutputActionControl>(actions);

		UiTestDriver.Step("テンプレートを編集する");
		await ClickByContentAsync(logOutput, "編集");
		var editor = await WaitForAsync(() => desktop.Windows.OfType<TemplateEditorDialog>().FirstOrDefault(w => w.IsVisible), "テンプレート編集ダイアログ");
		editor.FindControl<TextEditor>("Editor")!.Text = TemplateText;
		await ClickByContentAsync(editor, "OK");
		await WaitUntilAsync(() => !desktop.Windows.Contains(editor), "テンプレート編集ダイアログが閉じる");
		await WaitUntilAsync(() => ShownTemplate(logOutput) == TemplateText, "編集したテンプレートの表示");

		UiTestDriver.Step("テスト実行の結果を確認する");
		await ClickByTipAsync(page, "テスト実行");
		await WaitForTextAsync(logOutput, ExpectedOutput);

		UiTestDriver.Step("保存する");
		await ClickByTipAsync(page, SaveTip);
		var saved = await File.ReadAllTextAsync(workflowsFile);
		if (!saved.Contains("\"ApplicationStartup\"") || !saved.Contains(SavedActionType))
			throw new InvalidOperationException($"保存した内容にトリガーかアクションがありません: {saved}");

		UiTestDriver.Step("編集を破棄して保存した状態を読み戻す");
		(await WaitForControlAsync<TextBox>(page)).Text = "保存していない名前";
		await WaitForTextAsync(page, "保存していない名前");
		await ClickByTipAsync(page, RestoreTip);
		await WaitForTextAsync(page, WorkflowName);
		await WaitForTextAsync(page, TriggerName);
		await WaitForTextAsync(page, ActionName);
		logOutput = await WaitForControlAsync<LogOutputActionControl>(page);
		await WaitUntilAsync(() => ShownTemplate(logOutput) == TemplateText, "読み戻したテンプレートの表示");

		UiTestDriver.Step("確認ダイアログに答えて削除する");
		await ClickByTipAsync(page, "削除する");
		await ConfirmDialogAsync(settings.Window);
		await WaitForTextAsync(page, EmptyMessage);

		UiTestDriver.Step("削除した状態を保存する");
		await ClickByTipAsync(page, SaveTip);
		saved = await File.ReadAllTextAsync(workflowsFile);
		if (saved.Contains(SavedActionType))
			throw new InvalidOperationException($"削除したワークフローが保存されています: {saved}");
	}

	private static string? ShownTemplate(LogOutputActionControl control)
		=> control.GetVisualDescendants().OfType<TextBox>().FirstOrDefault()?.Text;
}
#endif
