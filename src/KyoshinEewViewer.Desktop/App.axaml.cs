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
	private IInterProcessCommunicationService? _ipcService;

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
			desktop.ShutdownMode = ShutdownMode.OnLastWindowClose;

			var splashWindow = new SplashWindow();
			desktop.MainWindow = splashWindow;
			splashWindow.Show();

			var config = Locator.Current.RequireService<KyoshinEewViewerConfiguration>();
			var subWindow = Locator.Current.RequireService<ISubWindowsService>();

			// プロセスの優先度設定
			if (config.AutoProcessPriority && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				try
				{
					Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
				}
				catch { }

			// クラッシュファイルのダンプ･再起動設定
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				WerRegisterAppLocalDump("./Dumps");
				RegisterApplicationRestart($"-c \"{Environment.CurrentDirectory.Replace("\"", "\\\"")}\" {(StartupOptions.Current?.StandaloneSeriesName is { } ssn ? $"-s {ssn.Replace("\"", "\\\"")}" : "")}", RestartFlags.None);
			}

			// NOTE: 旧バージョンからの移行
			if (config.Theme.WindowThemeName is string windowTheme)
			{
				config.Theme.WindowTheme = KyoshinEewViewerApp.Selector.WindowThemes?.FirstOrDefault(t => t.Meta.Identifier == windowTheme)?.Meta ?? config.Theme.WindowTheme;
				config.Theme.WindowThemeName = null; // 古い設定を削除
			}
			if (config.Theme.IntensityThemeName is string intensityTheme)
			{
				config.Theme.IntensityTheme = KyoshinEewViewerApp.Selector.IntensityThemes?.FirstOrDefault(t => t.Meta.Identifier == intensityTheme)?.Meta ?? config.Theme.IntensityTheme;
				config.Theme.IntensityThemeName = null; // 古い設定を削除
			}

			KyoshinEewViewerApp.Selector.ApplyTheme(config.Theme.WindowTheme, config.Theme.IntensityTheme);
			KyoshinEewViewerApp.Selector.WhenAnyValue(x => x.SelectedIntensityTheme)
				.Subscribe(x =>
				{
					if (x == null) return;
					config.Theme.IntensityTheme = x.Meta;
					Dispatcher.UIThread.Post(() => FixedObjectRenderer.UpdateIntensityPaintCache(desktop.Windows[0]));
				});

			Task.Run(async () =>
			{
				// 多重起動警告
				if (StartupOptions.Current?.StandaloneSeriesName is null &&
					Process.GetProcessesByName("KyoshinEewViewer.Desktop").Concat(Process.GetProcessesByName("KyoshinEewViewer")).Count(p => p.Responding) > 1)
				{
					// 設定に応じて処理を分岐
					if (config.FocusExistingInstanceOnDuplicate
#if DEBUG
					&& false // デバッグビルドでは無効化
#endif
					)
					{
						// 既存のウィンドウを最前面に表示
						using var ipcService = InterProcessCommunicationServiceFactory.Create();

						if (await ipcService.SendShowMainWindowMessageAsync())
						{
							await Dispatcher.UIThread.InvokeAsync(() =>
							{
								splashWindow.Close();
								desktop.Shutdown();
							});
							return;
						}
						// 通信に失敗した場合は警告ダイアログにフォールバック
					}

					// 警告ダイアログを表示
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
					await subWindow.ShowDialogSetupWizardWindow(async w =>
					{
						await Dispatcher.UIThread.InvokeAsync(() =>
						{
							desktop.MainWindow = w;
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
					desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
					KyoshinEewViewerApp.Selector.WhenAnyValue(x => x.SelectedWindowTheme).Where(x => x != null).Subscribe(x =>
					{
						if (x == null) return;
						config.Theme.WindowTheme = x.Meta;
						Dispatcher.UIThread.Post(() => FixedObjectRenderer.UpdateIntensityPaintCache(desktop.MainWindow));

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
					MainWindow.Opened += (s, e) =>
					{
						subWindow.SetupWizardWindow?.Close();
						splashWindow?.Close();
						splashWindow = null;

						// standaloneモードでない場合のみIPCサーバーを起動
						if (StartupOptions.Current?.StandaloneSeriesName is null)
						{
							_ipcService = InterProcessCommunicationServiceFactory.Create();
							_ipcService.StartServer();
						}
					};
					MainWindow.Show();
					MainWindow.Activate();
				});
			}).ConfigureAwait(false);

			desktop.Exit += (s, e) =>
			{
				MessageBus.Current.SendMessage(new ApplicationClosing());
				ConfigurationLoader.Save(config);
				_ipcService?.Dispose();
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
