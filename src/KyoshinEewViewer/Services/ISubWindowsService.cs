using KyoshinEewViewer.Core;
using KyoshinEewViewer.Series;
using KyoshinEewViewer.Views;
using System;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services;
public interface ISubWindowsService
{
	SettingWindow? SettingWindow { get; }
	SetupWizardWindow? SetupWizardWindow { get; }
	DebugWindow? DebugWindow { get; }

	Task ShowDialogSetupWizardWindow(Action<SetupWizardWindow> opened);
	void ShowSettingWindow();
	Task ShowDialogWindowThemeEditWindow(ThemeSelector.WindowTheme? theme);
	Task ShowDialogIntensityThemeEditWindow(ThemeSelector.IntensityTheme? theme);
	void ShowDebugWindow();

	/// <summary>
	/// Seriesを別ウィンドウで表示する
	/// </summary>
	void ShowSeriesWindow(SeriesBase series);

	/// <summary>
	/// Seriesウィンドウを閉じる
	/// </summary>
	void CloseSeriesWindow(SeriesBase series);

	/// <summary>
	/// Seriesが分離されているかどうかを取得する
	/// </summary>
	bool IsSeriesSeparated(SeriesBase series);

	/// <summary>
	/// すべてのSeriesウィンドウを閉じる
	/// </summary>
	void CloseAllSeriesWindows();

	/// <summary>
	/// Seriesウィンドウが開かれたときに発生するイベント
	/// </summary>
	event Action<SeriesBase>? SeriesWindowOpened;

	/// <summary>
	/// Seriesウィンドウが閉じられたときに発生するイベント
	/// </summary>
	event Action<SeriesBase>? SeriesWindowClosed;

	/// <summary>
	/// アプリケーション終了中かどうか
	/// trueの場合、サブウィンドウを閉じても設定から削除しない
	/// </summary>
	bool IsShuttingDown { get; set; }
}
