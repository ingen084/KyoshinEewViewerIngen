#if INTEGRATION_TEST
using Avalonia.Controls;
using Avalonia.VisualTree;
using KyoshinEewViewer.Views.SettingPages;
using System.Linq;
using System.Threading.Tasks;
using static KyoshinEewViewer.Desktop.IntegrationTest.UiOperations;

namespace KyoshinEewViewer.Desktop.IntegrationTest;

/// <summary>
/// 話者一覧をモックサーバーから取得し、メニューに表示できることを確認する
/// </summary>
internal static class VoicevoxScenario
{
	private const string SpeakerName = "テスト話者";

	public static async Task RunAsync(SettingWindowSession settings)
	{
		UiTestDriver.Step("VOICEVOX の話者一覧を取得する");
		var page = await settings.ShowPageAsync<VoicevoxPage>("VOICEVOX");
		await ClickByTipAsync(page, "話者一覧を更新");
		var button = await WaitForAsync(
			() => page.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.IsEffectivelyVisible && b.IsEnabled && b.Flyout is MenuFlyout),
			"話者の選択ボタン");

		UiTestDriver.Step("話者のメニューを確認する");
		await OpenMenuAsync(button, [SpeakerName]);
		var item = await WaitForAsync(
			() => ((MenuFlyout)button.Flyout!).Popup.Child?.GetVisualDescendants().OfType<MenuItem>().FirstOrDefault(),
			"話者の項目");
		await WaitUntilAsync(() => item.ItemCount == 2, "話者のスタイル2件");
		button.Flyout!.Hide();
	}
}
#endif
