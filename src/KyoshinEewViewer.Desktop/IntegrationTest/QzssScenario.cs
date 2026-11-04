#if INTEGRATION_TEST
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.VisualTree;
using KyoshinEewViewer.Series.Qzss;
using KyoshinEewViewer.ViewModels;
using KyoshinEewViewer.Views.SettingPages;
using System.Linq;
using System.Threading.Tasks;
using static KyoshinEewViewer.Desktop.IntegrationTest.UiOperations;

namespace KyoshinEewViewer.Desktop.IntegrationTest;

/// <summary>
/// デバッグメニューから災危通報の電文を入力し、受信履歴に表示されることを確認する
/// </summary>
internal static class QzssScenario
{
	private const string ReportHex = "9AAF8DED25000325BA00DA4A0F5AAC5A8000000008000000200000136DCCFB40";

	public static async Task RunAsync(IClassicDesktopStyleApplicationLifetime desktop, SettingWindowSession settings)
	{
		UiTestDriver.Step("災危通報のタブを開く");
		var mainWindow = desktop.MainWindow!;
		var viewModel = (MainViewModel)mainWindow.DataContext!;
		viewModel.SelectedSeries = viewModel.SeriesController.EnabledSeries.Single(s => s.Meta.Key == "qzss");
		var view = await WaitForControlAsync<QzssView>(mainWindow);

		UiTestDriver.Step("デバッグメニューから HEX を入力して送信する");
		var page = await settings.ShowPageAsync<DebugMenuPage>("デバッグメニュー");
		var hexBox = await WaitForAsync(
			() => page.GetVisualDescendants().OfType<TextBox>().Where(t => t.IsEffectivelyVisible).ElementAtOrDefault(1),
			"HEX の入力欄");
		hexBox.Text = ReportHex;
		await ClickByContentAsync(page, "送信");

		UiTestDriver.Step("受信履歴を確認する");
		await WaitForAsync(
			() => view.GetVisualDescendants().OfType<ListBox>().FirstOrDefault(l => l.IsEffectivelyVisible && l.ItemCount == 1),
			"災危通報の受信履歴 1件");
		await WaitForTextAsync(view, "EEW(警報)");
	}
}
#endif
