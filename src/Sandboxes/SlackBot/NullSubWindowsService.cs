using KyoshinEewViewer.Core;
using KyoshinEewViewer.Series;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.Views;
using System;
using System.Threading.Tasks;

namespace SlackBot;

// SlackBotではサブウィンドウ機能を使わないため、すべての操作を無視するダミー実装
// ISubWindowsService を要求するサービスがあるため、null ではなくこの実装を DI へ登録する
public class NullSubWindowsService : ISubWindowsService
{
	public SettingWindow? SettingWindow => null;
	public SetupWizardWindow? SetupWizardWindow => null;
	public DebugWindow? DebugWindow => null;

	public bool IsShuttingDown { get; set; }

	public event Action<SeriesBase>? SeriesWindowOpened { add { } remove { } }
	public event Action<SeriesBase>? SeriesWindowClosed { add { } remove { } }

	public Task ShowDialogSetupWizardWindow(Action<SetupWizardWindow> opened) => Task.CompletedTask;
	public void ShowSettingWindow() { }
	public Task ShowDialogWindowThemeEditWindow(ThemeSelector.WindowTheme? theme) => Task.CompletedTask;
	public Task ShowDialogIntensityThemeEditWindow(ThemeSelector.IntensityTheme? theme) => Task.CompletedTask;
	public void ShowDebugWindow() { }
	public void ShowSeriesWindow(SeriesBase series) { }
	public void CloseSeriesWindow(SeriesBase series) { }
	public bool IsSeriesSeparated(SeriesBase series) => false;
	public void CloseAllSeriesWindows() { }
}
