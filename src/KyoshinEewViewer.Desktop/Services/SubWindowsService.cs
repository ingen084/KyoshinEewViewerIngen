using Avalonia.Controls;
using Avalonia.Threading;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Desktop.Views;
using KyoshinEewViewer.ViewModels;
using ReactiveUI;
using Splat;
using System;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static KyoshinEewViewer.Desktop.NativeMethods;

namespace KyoshinEewViewer.Desktop.Services;

public class SubWindowsService
{
	public SettingWindow? SettingWindow { get; private set; }
	public SetupWizardWindow? SetupWizardWindow { get; private set; }

	public SubWindowsService()
	{
		SplatRegistrations.RegisterLazySingleton<SubWindowsService>();
	}

	private void ApplyTheme(Window window)
	{
		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || window.TryGetPlatformHandle()?.Handle is not { } handle)
			return;

		// Windowsにおけるウィンドウ周囲の色変更
		Avalonia.Media.Color FindColorResource(string name)
			=> (Avalonia.Media.Color)(window.FindResource(name) ?? throw new Exception($"リソース {name} が見つかりませんでした"));
		bool FindBoolResource(string name)
			=> (bool)(window.FindResource(name) ?? throw new Exception($"リソース {name} が見つかりませんでした"));

		var isDarkTheme = FindBoolResource("IsDarkTheme");
		var useDarkMode = isDarkTheme ? 1 : 0;
		DwmSetWindowAttribute(
			handle,
			Dwmwindowattribute.DwmwaUseImmersiveDarkMode,
			ref useDarkMode,
			Marshal.SizeOf(useDarkMode));

		var color = FindColorResource("TitleBackgroundColor");
		var intColor = color.R | color.G << 8 | color.B << 16;
		DwmSetWindowAttribute(
			handle,
			Dwmwindowattribute.DwmwaCaptionColor,
			ref intColor,
			Marshal.SizeOf(intColor));
	}
	private IDisposable Subscribe(Window window)
		=> KyoshinEewViewerApp.Selector.WhenAnyValue(x => x.SelectedWindowTheme).Where(x => x != null).Subscribe(x => ApplyTheme(window));

	public void ShowSettingWindow()
	{
		if (SettingWindow == null)
		{
			SettingWindow = new SettingWindow {
				DataContext = Locator.Current.RequireService<SettingWindowViewModel>()
			};
			var d = Subscribe(SettingWindow);
			ApplyTheme(SettingWindow);
			SettingWindow.Closed += (s, e) =>
			{
				d.Dispose();
				SettingWindow = null;
			};
		}
		if (App.MainWindow != null && App.MainWindow.IsVisible)
			SettingWindow.Show(App.MainWindow);
		else
			SettingWindow.Show();
	}
	public async Task ShowDialogSetupWizardWindow(Action<SetupWizardWindow> opened)
	{
		var mre = new ManualResetEventSlim(false);
		await Dispatcher.UIThread.InvokeAsync(() =>
		{
			if (SetupWizardWindow == null)
			{
				SetupWizardWindow = new SetupWizardWindow {
					DataContext = Locator.Current.RequireService<SetupWizardWindowViewModel>()
				};
				var d = Subscribe(SetupWizardWindow);
				ApplyTheme(SetupWizardWindow);
				SetupWizardWindow.Opened += (s, e) => opened(SetupWizardWindow);
				SetupWizardWindow.Closed += (s, e) =>
				{
					mre.Set();
					d.Dispose();
					SetupWizardWindow = null;
				};
				SetupWizardWindow.Continued += () => mre.Set();
			}
			SetupWizardWindow.Show();
		});
		await Task.Run(mre.Wait);
	}
}
