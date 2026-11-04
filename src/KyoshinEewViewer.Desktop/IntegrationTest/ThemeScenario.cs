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
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using static KyoshinEewViewer.Desktop.IntegrationTest.UiOperations;

namespace KyoshinEewViewer.Desktop.IntegrationTest;

/// <summary>
/// 外部テーマの読み込みと選択、テーマ編集画面の編集、破棄、保存を確認する
/// </summary>
internal static class ThemeScenario
{
	private const string PageTitle = "外観･基本設定";
	private const string SaveTip = "テーマを保存する";
	private const string ExternalThemeName = "UI test theme";
	private const string SavedIntensityThemeName = "UI test intensity";

	public static async Task RunAsync(IClassicDesktopStyleApplicationLifetime desktop, SettingWindowSession settings)
	{
		var page = await settings.ShowPageAsync<GeneralPage>(PageTitle);

		UiTestDriver.Step("外部テーマを選ぶ");
		var selector = await WaitForAsync(() => WindowThemeComboBox(page), "テーマの選択欄");
		selector.SelectedItem = selector.Items.OfType<ThemeSelector.WindowTheme>().FirstOrDefault(t => t.Theme.Name == ExternalThemeName)
			?? throw new InvalidOperationException($"外部テーマ「{ExternalThemeName}」が読み込まれていません");
		await WaitForTextAsync(selector, ExternalThemeName);

		UiTestDriver.Step("テーマを編集して変更を破棄する");
		Window window = await OpenEditWindowAsync<WindowThemeEditWindow>(desktop, page, 0);
		await EditColorAsync(window, CurrentWindowThemeType);
		window.Close();
		await ConfirmDialogAsync(window);
		await WaitUntilClosedAsync(desktop, window);
		await WaitUntilAsync(() => CurrentWindowThemeType() == ThemeType.ExternalFile, "元のテーマに戻る");
		await WaitForTextAsync(selector, ExternalThemeName);

		UiTestDriver.Step("外部テーマを編集して上書き保存する");
		window = await OpenEditWindowAsync<WindowThemeEditWindow>(desktop, page, 0);
		await EditColorAsync(window, CurrentWindowThemeType);
		await ClickByTipAsync(window, SaveTip);
		await ConfirmDialogAsync(window);
		await VerifySavedColorAsync(Path.Combine(PlatformDirectories.ApplicationData, "Themes", "ui-test-theme.json"), "TitleBackgroundColor");
		window.Close();
		await WaitUntilClosedAsync(desktop, window);
		await WaitUntilAsync(() => CurrentWindowThemeType() == ThemeType.ExternalFile, "保存したテーマの適用");

		UiTestDriver.Step("組み込みの震度テーマを編集して変更を破棄する");
		window = await OpenEditWindowAsync<IntensityThemeEditWindow>(desktop, page, 1);
		await EditColorAsync(window, CurrentIntensityThemeType);
		window.Close();
		await ConfirmDialogAsync(window);
		await WaitUntilClosedAsync(desktop, window);
		await WaitUntilAsync(() => CurrentIntensityThemeType() == ThemeType.BuiltIn, "元の震度テーマに戻る");

		UiTestDriver.Step("組み込みの震度テーマを別名で保存する");
		window = await OpenEditWindowAsync<IntensityThemeEditWindow>(desktop, page, 1);
		(await WaitForControlAsync<TextBox>(window)).Text = SavedIntensityThemeName;
		await EditColorAsync(window, CurrentIntensityThemeType);
		await ClickByTipAsync(window, SaveTip);
		await ConfirmDialogAsync(window);
		await VerifySavedNameAsync(Path.Combine(PlatformDirectories.ApplicationData, "IntensityThemes", $"{SavedIntensityThemeName}.json"));
		window.Close();
		await WaitUntilClosedAsync(desktop, window);
		await WaitUntilAsync(() => CurrentIntensityThemeType() == ThemeType.ExternalFile, "保存した震度テーマの適用");
		await WaitForTextAsync(page, SavedIntensityThemeName);
	}

	public static async Task VerifyAfterRestartAsync(IClassicDesktopStyleApplicationLifetime desktop, SettingWindowSession settings)
	{
		UiTestDriver.Step("再起動後のテーマを確認する");
		var page = await settings.ShowPageAsync<GeneralPage>(PageTitle);
		await WaitForTextAsync(await WaitForAsync(() => WindowThemeComboBox(page), "テーマの選択欄"), ExternalThemeName);
		await WaitForTextAsync(page, SavedIntensityThemeName);

		UiTestDriver.Step("再起動後のテーマの内容を確認する");
		Window window = await OpenEditWindowAsync<WindowThemeEditWindow>(desktop, page, 0);
		var picker = await WaitForControlAsync<ColorPicker>(window);
		await WaitUntilAsync(() => picker.Color == EditedColor, "保存した色");
		window.Close();
		await WaitUntilClosedAsync(desktop, window);
	}

	private static readonly Color EditedColor = Colors.Red;

	private static ThemeType? CurrentWindowThemeType() => KyoshinEewViewerApp.Selector.SelectedWindowTheme?.Meta.Type;
	private static ThemeType? CurrentIntensityThemeType() => KyoshinEewViewerApp.Selector.SelectedIntensityTheme?.Meta.Type;

	private static async Task<TWindow> OpenEditWindowAsync<TWindow>(IClassicDesktopStyleApplicationLifetime desktop, GeneralPage page, int editButtonIndex) where TWindow : Window
	{
		await ClickByContentAsync(page, "編集", editButtonIndex);
		var window = await WaitForAsync(() => desktop.Windows.OfType<TWindow>().FirstOrDefault(w => w.IsVisible), typeof(TWindow).Name);
		await WaitForControlAsync<ScrollViewer>(window);
		await ExpandAllAsync(window);
		return window;
	}

	/// <summary>
	/// 色を変更し、編集中のテーマが適用されるのを待つ
	/// </summary>
	private static async Task EditColorAsync(Window window, Func<ThemeType?> currentThemeType)
	{
		(await WaitForControlAsync<ColorPicker>(window)).Color = EditedColor;
		await WaitUntilAsync(() => currentThemeType() == ThemeType.Temporary, "編集中のテーマの適用");
	}

	private static Task WaitUntilClosedAsync(IClassicDesktopStyleApplicationLifetime desktop, Window window)
		=> WaitUntilAsync(() => !desktop.Windows.Contains(window), "テーマ編集画面が閉じる");

	private static Task VerifySavedColorAsync(string path, string propertyName)
		=> WaitUntilAsync(
			() => File.Exists(path) && Color.Parse(JsonDocument.Parse(File.ReadAllText(path)).RootElement.GetProperty(propertyName).GetString()!) == EditedColor,
			$"{Path.GetFileName(path)} に保存した色");

	private static Task VerifySavedNameAsync(string path)
		=> WaitUntilAsync(
			() => File.Exists(path) && JsonDocument.Parse(File.ReadAllText(path)).RootElement.GetProperty("Name").GetString() == SavedIntensityThemeName,
			$"{Path.GetFileName(path)} の保存");

	private static ComboBox? WindowThemeComboBox(GeneralPage page)
		=> page.GetVisualDescendants().OfType<ComboBox>().FirstOrDefault(c => c.IsEffectivelyVisible && c.Items.OfType<ThemeSelector.WindowTheme>().Any());
}
#endif
