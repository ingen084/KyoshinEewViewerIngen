#if INTEGRATION_TEST
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.VisualTree;
using AvaloniaEdit;
using CommunityToolkit.Mvvm.Messaging;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.Services.Workflows.BuiltinActions;
using KyoshinEewViewer.ViewModels;
using KyoshinEewViewer.Views.Components;
using KyoshinEewViewer.Views.SettingPages;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static KyoshinEewViewer.Desktop.IntegrationTest.UiOperations;

namespace KyoshinEewViewer.Desktop.IntegrationTest;

/// <summary>
/// ワークフローの作成、テスト実行、保存、読み戻し、削除までを設定画面から行う
/// </summary>
internal static class WorkflowScenario
{
	private const string EmptyMessage = "ワークフローがまだありません。";
	private const string WorkflowName = "新しいワークフロー";
	private const string TriggerName = "アプリケーション起動時";
	private const string ActionName = "ログ出力";
	private const string SaveTip = "ワークフローを保存する";
	private const string TemplateText = "UI-CHECK|{{ EventType }}|{{ \"05\" | string.slice 1 }}";
	private const string ExpectedOutput = "UI-CHECK|ApplicationStartup|5";
	private const string SavedActionType = "\"LogOutput\"";

	public static async Task RunAsync(IClassicDesktopStyleApplicationLifetime desktop, string workflowsFile)
	{
		UiTestDriver.Step("設定ウィンドウのワークフロー画面を開く");
		var subWindows = ServiceLocator.Current.RequireService<ISubWindowsService>();
		StrongReferenceMessenger.Default.Send(new ShowSettingWindowRequested());
		var window = await WaitForAsync(() => subWindows.SettingWindow is { IsVisible: true } w ? w : null, "設定ウィンドウ");
		var viewModel = (SettingWindowViewModel)window.DataContext!;
		viewModel.SelectedSettingPage = viewModel.SettingPages.Single(p => p.Title == "ワークフロー");
		var page = await WaitForControlAsync<WorkflowPage>(window);
		await WaitForTextAsync(page, EmptyMessage);

		UiTestDriver.Step("ワークフローを追加する");
		await ClickByTipAsync(page, "新しいワークフローを作成");
		await WaitForTextAsync(page, WorkflowName);

		UiTestDriver.Step("トリガーを選ぶ");
		var triggerButton = await WaitForAsync(() => FindMenuButton(page), "トリガーの選択ボタン");
		await ChooseMenuItemAsync(triggerButton, WorkflowService.AllTriggers.Select(t => t.DisplayName), TriggerName);
		await WaitForTextAsync(page, "アプリケーション起動完了時に一度だけトリガーされます。");

		UiTestDriver.Step("アクションを追加して選ぶ");
		var actions = await WaitForControlAsync<MultipleActionControl>(page);
		await ClickByTipAsync(actions, "アクションを追加");
		var actionButton = await WaitForAsync(() => FindMenuButton(actions), "アクションの選択ボタン");
		await ChooseMenuItemAsync(actionButton, WorkflowService.AllActions.Select(a => a.DisplayName), ActionName);
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
		await ClickByTipAsync(page, "保存した状態を復元する(ファイルの状態を読み戻す)");
		await WaitForTextAsync(page, WorkflowName);
		await WaitForTextAsync(page, TriggerName);
		await WaitForTextAsync(page, ActionName);
		logOutput = await WaitForControlAsync<LogOutputActionControl>(page);
		await WaitUntilAsync(() => ShownTemplate(logOutput) == TemplateText, "読み戻したテンプレートの表示");

		UiTestDriver.Step("確認ダイアログに答えて削除する");
		await ClickByTipAsync(page, "削除する");
		await ConfirmDialogAsync(window);
		await WaitForTextAsync(page, EmptyMessage);

		UiTestDriver.Step("削除した状態を保存する");
		await ClickByTipAsync(page, SaveTip);
		saved = await File.ReadAllTextAsync(workflowsFile);
		if (saved.Contains(SavedActionType))
			throw new InvalidOperationException($"削除したワークフローが保存されています: {saved}");

		UiTestDriver.Step("設定ウィンドウを閉じる");
		window.Close();
		await WaitUntilAsync(() => subWindows.SettingWindow == null, "設定ウィンドウが閉じる");
	}

	private static Button? FindMenuButton(Visual root)
		=> root.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.IsEffectivelyVisible && b.Flyout is MenuFlyout);

	private static string? ShownTemplate(LogOutputActionControl control)
		=> control.GetVisualDescendants().OfType<TextBox>().FirstOrDefault()?.Text;
}
#endif
