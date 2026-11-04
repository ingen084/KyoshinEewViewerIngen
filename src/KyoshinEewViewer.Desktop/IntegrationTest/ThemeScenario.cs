#if INTEGRATION_TEST
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.VisualTree;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Views;
using KyoshinEewViewer.Views.SettingPages;
using System;
using System.Linq;
using System.Threading.Tasks;
using static KyoshinEewViewer.Desktop.IntegrationTest.UiOperations;

namespace KyoshinEewViewer.Desktop.IntegrationTest;

/// <summary>
/// 外部テーマの読み込みと選択、テーマ編集画面の操作を確認する
/// </summary>
/// <remarks>
/// テーマ編集画面の保存はユーザーの設定ディレクトリへ書き込むので押さない
/// </remarks>
internal static class ThemeScenario
{
	private const string PageTitle = "外観･基本設定";
	private const string ExternalThemeName = "UI test theme";

	public static async Task RunAsync(IClassicDesktopStyleApplicationLifetime desktop, SettingWindowSession settings)
	{
		var page = await settings.ShowPageAsync<GeneralPage>(PageTitle);

		UiTestDriver.Step("外部テーマを選ぶ");
		var selector = await WaitForAsync(() => WindowThemeComboBox(page), "テーマの選択欄");
		selector.SelectedItem = selector.Items.OfType<ThemeSelector.WindowTheme>().FirstOrDefault(t => t.Theme.Name == ExternalThemeName)
			?? throw new InvalidOperationException($"外部テーマ「{ExternalThemeName}」が読み込まれていません");
		await WaitForTextAsync(selector, ExternalThemeName);

		UiTestDriver.Step("テーマを編集して変更を破棄する");
		await EditAndDiscardAsync<WindowThemeEditWindow>(desktop, settings, page, 0, () => KyoshinEewViewerApp.Selector.SelectedWindowTheme?.Meta.Type);
		await WaitForTextAsync(selector, ExternalThemeName);

		UiTestDriver.Step("震度テーマを編集して変更を破棄する");
		await EditAndDiscardAsync<IntensityThemeEditWindow>(desktop, settings, page, 1, () => KyoshinEewViewerApp.Selector.SelectedIntensityTheme?.Meta.Type);
	}

	public static async Task VerifyAfterRestartAsync(SettingWindowSession settings)
	{
		UiTestDriver.Step("再起動後のテーマを確認する");
		var page = await settings.ShowPageAsync<GeneralPage>(PageTitle);
		await WaitForTextAsync(await WaitForAsync(() => WindowThemeComboBox(page), "テーマの選択欄"), ExternalThemeName);
	}

	private static async Task EditAndDiscardAsync<TWindow>(
		IClassicDesktopStyleApplicationLifetime desktop, SettingWindowSession settings, GeneralPage page, int editButtonIndex, Func<ThemeType?> currentThemeType) where TWindow : Window
	{
		var original = currentThemeType();
		await ClickByContentAsync(page, "編集", editButtonIndex);
		var window = await WaitForAsync(() => desktop.Windows.OfType<TWindow>().FirstOrDefault(w => w.IsVisible), typeof(TWindow).Name);
		await WaitForControlAsync<ScrollViewer>(window);
		await ExpandAllAsync(window);
		var picker = await WaitForControlAsync<ColorPicker>(window);

		picker.Color = Colors.Red;
		await WaitUntilAsync(() => currentThemeType() == ThemeType.Temporary, "編集中のテーマの適用");

		window.Close();
		await ConfirmDialogAsync(window);
		await WaitUntilAsync(() => !desktop.Windows.Contains(window), "テーマ編集画面が閉じる");
		await WaitUntilAsync(() => currentThemeType() == original, "元のテーマに戻る");
	}

	private static ComboBox? WindowThemeComboBox(GeneralPage page)
		=> page.GetVisualDescendants().OfType<ComboBox>().FirstOrDefault(c => c.IsEffectivelyVisible && c.Items.OfType<ThemeSelector.WindowTheme>().Any());
}
#endif
