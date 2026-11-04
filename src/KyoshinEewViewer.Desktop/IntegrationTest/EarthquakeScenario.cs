#if INTEGRATION_TEST
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.VisualTree;
using KyoshinEewViewer.Series.Earthquake;
using KyoshinEewViewer.ViewModels;
using System.Linq;
using System.Threading.Tasks;
using static KyoshinEewViewer.Desktop.IntegrationTest.UiOperations;

namespace KyoshinEewViewer.Desktop.IntegrationTest;

/// <summary>
/// モックサーバーの JMA フィードから電文を受信し、地震情報の画面に表示されることを確認する
/// </summary>
internal static class EarthquakeScenario
{
	public static async Task RunAsync(IClassicDesktopStyleApplicationLifetime desktop)
	{
		UiTestDriver.Step("地震情報のタブを開く");
		var mainWindow = desktop.MainWindow!;
		var viewModel = (MainViewModel)mainWindow.DataContext!;
		viewModel.SelectedSeries = viewModel.SeriesController.EnabledSeries.Single(s => s.Meta.Key == "earthquake");
		var view = await WaitForControlAsync<EarthquakeView>(mainWindow);

		UiTestDriver.Step("フィードから受信した最新の地震の内容を確認する");
		await WaitForTextAsync(view, "石川県能登地方");
		await WaitForTextAsync(view, "5.1");
		await WaitForTextAsync(view, "10");
		await WaitForTextAsync(view, "石川県");

		UiTestDriver.Step("一覧から別の地震を選ぶ");
		var list = await WaitForAsync(
			() => view.GetVisualDescendants().OfType<ListBox>().FirstOrDefault(l => l.IsEffectivelyVisible && l.ItemCount == 2),
			"地震情報の一覧 2件");
		list.SelectedIndex = 1;
		await WaitForTextAsync(view, "4.2");
		await WaitForTextAsync(view, "50");
		await WaitForTextAsync(view, "茨城県");

		UiTestDriver.Step("一覧から最新の地震に戻す");
		list.SelectedIndex = 0;
		await WaitForTextAsync(view, "5.1");
	}
}
#endif
