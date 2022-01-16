using KyoshinEewViewer.ViewModels;
using KyoshinEewViewer.Views;

namespace KyoshinEewViewer.Services;

public class SubWindowsService
{
	public static SubWindowsService Default { get; } = new SubWindowsService();

	public SettingWindow? SettingWindow { get; private set; }
	public UpdateWindow? UpdateWindow { get; private set; }

	public void ShowSettingWindow()
	{
		if (SettingWindow == null)
		{
			SettingWindow = new SettingWindow
			{
				DataContext = new SettingWindowViewModel()
			};
			SettingWindow.Closed += (s, e) => SettingWindow = null;
		}
		SettingWindow.Show(App.MainWindow);
	}
	public void ShowUpdateWindow()
	{
		if (UpdateWindow == null)
		{
			UpdateWindow = new UpdateWindow
			{
				DataContext = new UpdateWindowViewModel()
			};
			UpdateWindow.Closed += (s, e) => UpdateWindow = null;
		}
		UpdateWindow.Show(App.MainWindow);
	}
}
