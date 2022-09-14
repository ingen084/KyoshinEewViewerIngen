using Avalonia.Controls;
using KyoshinEewViewer.ViewModels;
using KyoshinEewViewer.Views;
using ReactiveUI;
using System;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static KyoshinEewViewer.NativeMethods;

namespace KyoshinEewViewer.Services;

public class SubWindowsService
{
	public static SubWindowsService Default { get; } = new SubWindowsService();

	public SettingWindow? SettingWindow { get; private set; }
	public SetupWizardWindow? SetupWizardWindow { get; private set; }

	private void ApplyTheme(Window window)
	{
		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || window.PlatformImpl is null)
			return;

		Avalonia.Media.Color FindColorResource(string name)
			=> (Avalonia.Media.Color)(window.FindResource(name) ?? throw new Exception($"リソース {name} が見つかりませんでした"));
		bool FindBoolResource(string name)
			=> (bool)(window.FindResource(name) ?? throw new Exception($"リソース {name} が見つかりませんでした"));

		var isDarkTheme = FindBoolResource("IsDarkTheme");
		var USE_DARK_MODE = isDarkTheme ? 1 : 0;
		DwmSetWindowAttribute(
			window.PlatformImpl.Handle.Handle,
			DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE,
			ref USE_DARK_MODE,
			Marshal.SizeOf(USE_DARK_MODE));

		var color = FindColorResource("TitleBackgroundColor");
		var colord = color.R | color.G << 8 | color.B << 16;
		DwmSetWindowAttribute(
			window.PlatformImpl.Handle.Handle,
			DWMWINDOWATTRIBUTE.DWMWA_CAPTION_COLOR,
			ref colord,
			Marshal.SizeOf(colord));
	}
	private IDisposable Subscribe(Window window)
		=> App.Selector.WhenAnyValue(x => x.SelectedWindowTheme).Where(x => x != null).Subscribe(x => ApplyTheme(window));

	public void ShowSettingWindow()
	{
		if (SettingWindow == null)
		{
			SettingWindow = new()
			{
				DataContext = new SettingWindowViewModel()
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
	public async Task ShowDialogSetupWizardWindow(Action opened)
	{
		var mre = new ManualResetEventSlim(false);
		if (SetupWizardWindow == null)
		{
			SetupWizardWindow = new()
			{
				DataContext = new SetupWizardWindowViewModel()
			};
			var d = Subscribe(SetupWizardWindow);
			ApplyTheme(SetupWizardWindow);
			SetupWizardWindow.Opened += (s, e) => opened();
			SetupWizardWindow.Closed += (s, e) =>
			{
				mre.Set();
				d.Dispose();
				SetupWizardWindow = null;
			};
			SetupWizardWindow.Continued += () => mre.Set();
		}
		SetupWizardWindow.Show();
		await Task.Run(() => mre.Wait());
	}
}
