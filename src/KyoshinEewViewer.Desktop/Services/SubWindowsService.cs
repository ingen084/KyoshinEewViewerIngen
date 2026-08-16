using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.ViewModels;
using KyoshinEewViewer.Views;
using R3;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.Linq;
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

	public event Action<SeriesBase>? SeriesWindowOpened;
	public event Action<SeriesBase>? SeriesWindowClosed;
	public bool IsShuttingDown { get; set; }

	private readonly Dictionary<string, SeriesWindow> _seriesWindows = [];

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
		=> KyoshinEewViewerApp.Selector.ObservePropertyChanged(x => x.SelectedWindowTheme).Where(x => x != null).Subscribe(x => ApplyTheme(window));

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

	public void ShowSeriesWindow(SeriesBase series)
	{
		if (_seriesWindows.TryGetValue(series.Meta.Key, out var existingWindow))
		{
			existingWindow.Activate();
			return;
		}

		var config = Locator.Current.RequireService<KyoshinEewViewerConfiguration>();
		var viewModel = new SeriesWindowViewModel(series, config);

		var window = new SeriesWindow { DataContext = viewModel };

		if (!config.MultiWindow.SeriesWindows.TryGetValue(series.Meta.Key, out var windowConfig))
		{
			windowConfig = new();
			config.MultiWindow.SeriesWindows[series.Meta.Key] = windowConfig;
		}

		// ウィンドウ位置の復元
		if (windowConfig.WindowSize is { } size)
		{
			window.Width = size.X;
			window.Height = size.Y;
		}
		window.WindowState = windowConfig.WindowState;
		if (windowConfig.WindowLocation is { } loc && WindowPlacementTracker.IsValidLocation(window, loc))
		{
			window.WindowStartupLocation = WindowStartupLocation.Manual;
			window.Position = new PixelPoint((int)loc.X, (int)loc.Y);
		}

		var themeSubscription = Subscribe(window);
		ApplyTheme(window);

		var placementTracker = new WindowPlacementTracker(window, windowConfig);

		window.Closing += (s, e) => placementTracker.Save();

		window.Closed += (s, e) =>
		{
			// アプリケーション終了中でない場合のみ「開いている」状態を解除
			if (!IsShuttingDown)
				windowConfig.IsOpen = false;

			placementTracker.Dispose();
			themeSubscription.Dispose();
			_seriesWindows.Remove(series.Meta.Key);
			viewModel.DetachFromSeries();

			SeriesWindowClosed?.Invoke(series);
		};

		_seriesWindows[series.Meta.Key] = window;
		windowConfig.IsOpen = true;

		viewModel.AttachToSeries();

		SeriesWindowOpened?.Invoke(series);

		// オーナーを設定しない（メインウィンドウより常に表に表示しないようにする）
		window.Show();
	}

	public void CloseSeriesWindow(SeriesBase series)
	{
		if (_seriesWindows.TryGetValue(series.Meta.Key, out var window))
			window.Close();
	}

	public bool IsSeriesSeparated(SeriesBase series)
		=> _seriesWindows.ContainsKey(series.Meta.Key);

	public void CloseAllSeriesWindows()
	{
		foreach (var window in _seriesWindows.Values.ToArray())
			window.Close();
	}
}
