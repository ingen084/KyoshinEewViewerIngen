#if INTEGRATION_TEST
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.Views.SettingPages;
using System.Linq;
using System.Threading.Tasks;
using static KyoshinEewViewer.Desktop.IntegrationTest.UiOperations;

namespace KyoshinEewViewer.Desktop.IntegrationTest;

/// <summary>
/// ワークフロー画面の共通の操作
/// </summary>
internal static class WorkflowPageOperations
{
	public const string PageTitle = "ワークフロー";
	public const string EmptyMessage = "ワークフローがまだありません。";
	public const string SaveTip = "ワークフローを保存する";
	public const string RestoreTip = "保存した状態を復元する(ファイルの状態を読み戻す)";

	/// <summary>
	/// ワークフローを追加し、画面が新しいワークフローの内容に切り替わるのを待つ
	/// </summary>
	public static async Task AddWorkflowAsync(WorkflowPage page)
	{
		await ClickByTipAsync(page, "新しいワークフローを作成");
		await WaitForTextStartingWithAsync(page, "何もしないトリガーです。");
	}

	public static async Task ChooseTriggerAsync(WorkflowPage page, string name)
	{
		var button = await WaitForAsync(() => MenuButtons(page).FirstOrDefault(), "トリガーの選択ボタン");
		await ChooseMenuItemAsync(button, WorkflowService.AllTriggers.Select(t => t.DisplayName), name);
	}

	/// <summary>
	/// アクションを末尾に追加して選ぶ
	/// </summary>
	public static async Task AddActionAsync(Visual actions, string name)
	{
		var count = MenuButtons(actions).Length;
		await ClickByTipAsync(actions, "アクションを追加");
		var button = await WaitForAsync(() => MenuButtons(actions) is { } buttons && buttons.Length > count ? buttons[^1] : null, "アクションの選択ボタン");
		await ChooseMenuItemAsync(button, WorkflowService.AllActions.Select(a => a.DisplayName), name);
	}

	private static Button[] MenuButtons(Visual root)
		=> [.. root.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && b.Flyout is MenuFlyout)];
}
#endif
