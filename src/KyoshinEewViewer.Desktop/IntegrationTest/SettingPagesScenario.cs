#if INTEGRATION_TEST
using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Series;
using KyoshinEewViewer.Series.KyoshinMonitor.SettingPages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static KyoshinEewViewer.Desktop.IntegrationTest.UiOperations;

namespace KyoshinEewViewer.Desktop.IntegrationTest;

/// <summary>
/// 設定画面のすべてのページを開く。表示の失敗とバインディングエラーを検出する
/// </summary>
internal static class SettingPagesScenario
{
	public static async Task RunAsync(SettingWindowSession settings)
	{
		var host = (ContentControl)(await WaitForControlAsync<FANavigationView>(settings.Window)).Content!;

		// 項目を束ねるだけのページは選択できない
		foreach (var page in Flatten(settings.ViewModel.SettingPages).Where(p => p.IsVisible && p is not BasicSettingPage))
		{
			UiTestDriver.Step($"設定ページ「{page.Title}」を開く");
			var previous = host.Content;
			settings.ViewModel.SelectedSettingPage = page;
			await WaitUntilAsync(() => host.Content != previous && host.Content is Control { IsEffectivelyVisible: true }, $"設定ページ「{page.Title}」");
			await ExpandAllAsync((Control)host.Content!);
		}

		UiTestDriver.Step("災危通報の設定の選択欄を確認する");
		settings.ViewModel.SelectedSettingPage = Flatten(settings.ViewModel.SettingPages).Single(p => p.Title == "みちびき 災危通報");
		await WaitForTextAsync(settings.Window, "シリアルポート");

		await VerifyKyoshinMonitorPagesAsync(settings);
	}

	/// <summary>
	/// 強震モニタを有効にすると防災科研のサーバーへ接続するので、ページだけを別のウィンドウで表示する
	/// </summary>
	private static async Task VerifyKyoshinMonitorPagesAsync(SettingWindowSession settings)
	{
		UiTestDriver.Step("強震モニタの設定の選択欄を確認する");
		await ShowInWindowAsync(settings, new KyoshinMonitorPage(), ["強震モニタ", "普通(震度1程度以上)", "表示しない", "点滅"]);

		UiTestDriver.Step("緊急地震速報の設定の選択欄を確認する");
		await ShowInWindowAsync(settings, new KyoshinMonitorEewPage(), ["震度5弱以上"]);
	}

	private static async Task ShowInWindowAsync(SettingWindowSession settings, Control page, string[] expectedTexts)
	{
		var window = new Window { DataContext = settings.ViewModel, Content = page, Width = 800, Height = 2000 };
		window.Show();
		await ExpandAllAsync(page);
		foreach (var text in expectedTexts)
			await WaitForTextAsync(page, text);
		window.Close();
	}

	private static IEnumerable<ISettingPage> Flatten(IEnumerable<ISettingPage> pages)
		=> pages.SelectMany(p => Flatten(p.SubPages).Prepend(p));
}
#endif
