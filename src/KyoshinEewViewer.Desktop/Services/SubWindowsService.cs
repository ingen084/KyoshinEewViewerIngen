using Avalonia.Controls;
using Avalonia.Threading;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.ViewModels;
using KyoshinEewViewer.Views;
using ReactiveUI;
using Splat;
using System;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static KyoshinEewViewer.Desktop.NativeMethods;

namespace KyoshinEewViewer.Desktop.Services;

public class SubWindowsService : ISubWindowsService
{
	public SettingWindow? SettingWindow { get; private set; }
	public SetupWizardWindow? SetupWizardWindow { get; private set; }
	public WindowThemeEditWindow? WindowThemeEditWindow { get; private set; }
	public IntensityThemeEditWindow? IntensityThemeEditWindow { get; private set; }
	public DebugWindow? DebugWindow { get; private set; }

	public SubWindowsService()
	{
		SplatRegistrations.RegisterLazySingleton<ISubWindowsService, SubWindowsService>();
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
			SettingWindow = new SettingWindow
			{
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
				SetupWizardWindow = new SetupWizardWindow
				{
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

	public async Task ShowDialogWindowThemeEditWindow(ThemeSelector.WindowTheme? theme)
	{
		var mre = new ManualResetEventSlim(false);
		await Dispatcher.UIThread.InvokeAsync(() =>
		{
			if (WindowThemeEditWindow == null)
			{
				WindowThemeEditWindow = new()
				{
					WindowTheme = theme
				};
				var d = Subscribe(WindowThemeEditWindow);
				ApplyTheme(WindowThemeEditWindow);
				WindowThemeEditWindow.Closed += (s, e) =>
				{
					mre.Set();
					d.Dispose();
					WindowThemeEditWindow = null;
				};
			}
			var targetWindow = SettingWindow ?? App.MainWindow;
			if (targetWindow != null && targetWindow.IsVisible)
				WindowThemeEditWindow.ShowDialog(targetWindow);
			else
				WindowThemeEditWindow.Show();
		});
		await Task.Run(mre.Wait);
	}

	public async Task ShowDialogIntensityThemeEditWindow(ThemeSelector.IntensityTheme? theme)
	{
		var mre = new ManualResetEventSlim(false);
		await Dispatcher.UIThread.InvokeAsync(() =>
		{
			if (IntensityThemeEditWindow == null)
			{
				IntensityThemeEditWindow = new()
				{
					IntensityTheme = theme
				};
				var d = Subscribe(IntensityThemeEditWindow);
				ApplyTheme(IntensityThemeEditWindow);
				IntensityThemeEditWindow.Closed += (s, e) =>
				{
					mre.Set();
					d.Dispose();
					IntensityThemeEditWindow = null;
				};
			}
			var targetWindow = SettingWindow ?? App.MainWindow;
			if (targetWindow != null && targetWindow.IsVisible)
				IntensityThemeEditWindow.ShowDialog(targetWindow);
			else
				IntensityThemeEditWindow.Show();
		});
		await Task.Run(mre.Wait);
	}

	public void ShowDebugWindow()
	{
		if (DebugWindow == null)
		{
			var vm = Locator.Current.RequireService<DebugWindowViewModel>();
			DebugWindow = new DebugWindow
			{
				DataContext = vm
			};

			var d = Subscribe(DebugWindow);
			ApplyTheme(DebugWindow);
			DebugWindow.Closed += (s, e) =>
			{
				d.Dispose();
				DebugWindow = null;
			};
		}
		if (App.MainWindow != null && App.MainWindow.IsVisible)
			DebugWindow.Show(App.MainWindow);
		else
			DebugWindow.Show();
	}
}
