using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Desktop.Services;
using KyoshinEewViewer.Desktop.Views;
using KyoshinEewViewer.Series;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.ViewModels;
using KyoshinEewViewer.Views;
using ReactiveUI;
using Splat;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static KyoshinEewViewer.Desktop.NativeMethods;

namespace KyoshinEewViewer.Desktop;

public class App : Application
{
	private static Window? _mainWindow;
	public static Window? MainWindow
	{
		get => _mainWindow;
		set {
			_mainWindow = value;
			KyoshinEewViewerApp.TopLevelControl = value;
		}
	}

	public override void Initialize() => AvaloniaXamlLoader.Load(this);

	public override void OnFrameworkInitializationCompleted()
	{
		KyoshinEewViewerApp.Application = this;

		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			KyoshinEewViewerApp.Selector = ThemeSelector.Create(".");
			KyoshinEewViewerApp.Selector.EnableThemes(this);

			var splashWindow = new SplashWindow();
			splashWindow.Show();

			var config = Locator.Current.RequireService<KyoshinEewViewerConfiguration>();
			var subWindow = Locator.Current.RequireService<SubWindowsService>();

			// プロセスの優先度設定
			if (config.AutoProcessPriority)
				Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;

			// クラッシュファイルのダンプ･再起動設定
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				WerRegisterAppLocalDump("./Dumps");
				RegisterApplicationRestart($"-c \"{Environment.CurrentDirectory.Replace("\"", "\\\"")}\" {(StartupOptions.Current?.StandaloneSeriesName is { } ssn ? $"-s {ssn.Replace("\"", "\\\"")}" : "")}", RestartFlags.None);
			}

			KyoshinEewViewerApp.Selector.ApplyTheme(config.Theme.WindowThemeName, config.Theme.IntensityThemeName);
			KyoshinEewViewerApp.Selector.WhenAnyValue(x => x.SelectedIntensityTheme).Where(x => x != null)
				.Subscribe(x =>
				{
					config.Theme.IntensityThemeName = x?.Name ?? "Standard";
					FixedObjectRenderer.UpdateIntensityPaintCache(desktop.Windows[0]);
				});

			Task.Run(async () =>
			{
				// 多重起動警告
				if (StartupOptions.Current?.StandaloneSeriesName is null && Process.GetProcessesByName("KyoshinEewViewer.Desktop").Count(p => p.Responding) > 1)
				{
					var mre = new ManualResetEventSlim(false);
					DuplicateInstanceWarningWindow? dialog = null;
					await Dispatcher.UIThread.InvokeAsync(() =>
					{
						dialog = new DuplicateInstanceWarningWindow();
						dialog.Closed += (s, e) => mre.Set();
						dialog.Show(splashWindow);
					});
					mre.Wait();
					if (!dialog?.IsContinue ?? false)
					{
						await Dispatcher.UIThread.InvokeAsync(() =>
						{
							splashWindow.Close();
							desktop.Shutdown();
						});
						return;
					}
				}

				// ウィザード表示
				if (
					config.ShowWizard &&
					StartupOptions.Current?.StandaloneSeriesName is null
				)
				{
					await subWindow.ShowDialogSetupWizardWindow(async () =>
					{
						await Task.Delay(200);
						await Dispatcher.UIThread.InvokeAsync(() =>
						{
							splashWindow?.Close();
							splashWindow = null;
						});
					});
					config.ShowWizard = false;
					ConfigurationLoader.Save(config);
				}

				await Dispatcher.UIThread.InvokeAsync(() =>
				{
					desktop.MainWindow = MainWindow = new MainWindow
					{
						DataContext = Locator.Current.RequireService<MainViewModel>(),
					};
					KyoshinEewViewerApp.Selector.WhenAnyValue(x => x.SelectedWindowTheme).Where(x => x != null).Subscribe(x =>
					{
						config.Theme.WindowThemeName = x?.Name ?? "Light";
						FixedObjectRenderer.UpdateIntensityPaintCache(desktop.MainWindow);

						if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || desktop.MainWindow.TryGetPlatformHandle()?.Handle is not { } handle)
							return;
						// Windowsにおけるウィンドウ周囲の色変更
						Avalonia.Media.Color FindColorResource(string name)
							=> (Avalonia.Media.Color)(desktop.MainWindow.FindResource(name) ?? throw new Exception($"リソース {name} が見つかりませんでした"));
						bool FindBoolResource(string name)
							=> (bool)(desktop.MainWindow.FindResource(name) ?? throw new Exception($"リソース {name} が見つかりませんでした"));

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
					});
					MainWindow.Opened += async (s, e) =>
					{
						await Task.Delay(1000);
						subWindow.SetupWizardWindow?.Close();
						splashWindow?.Close();
						splashWindow = null;
					};
					MainWindow.Show();
					MainWindow.Activate();
				});
			}).ConfigureAwait(false);

			desktop.Exit += (s, e) =>
			{
				MessageBus.Current.SendMessage(new ApplicationClosing());
				ConfigurationLoader.Save(config);
			};
		}

		base.OnFrameworkInitializationCompleted();
	}

	/// <summary>
	/// override RegisterServices register custom service
	/// </summary>
	public override void RegisterServices()
	{
		Locator.CurrentMutable.RegisterLazySingleton(ConfigurationLoader.Load, typeof(KyoshinEewViewerConfiguration));
		Locator.CurrentMutable.RegisterLazySingleton(() => new SeriesController(), typeof(SeriesController));
		var config = Locator.Current.RequireService<KyoshinEewViewerConfiguration>();
		LoggingAdapter.Setup(config);

		SetupIOC(Locator.GetLocator());
		base.RegisterServices();
	}

	public void OpenSettingsClicked(object sender, EventArgs args)
		=> MessageBus.Current.SendMessage(new ShowSettingWindowRequested());

	public static void SetupIOC(IDependencyResolver resolver)
	{
		KyoshinEewViewerApp.SetupIOC(resolver);
		SplatRegistrations.SetupIOC(resolver);
	}
}
