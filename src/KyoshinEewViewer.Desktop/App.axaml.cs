using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Desktop.Services;
using KyoshinEewViewer.Desktop.Views;
using KyoshinEewViewer.Notification;
using KyoshinEewViewer.Series;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.ViewModels;
using R3;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static KyoshinEewViewer.Desktop.NativeMethods;
using Microsoft.Extensions.DependencyInjection;

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

	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);
#if DEBUG
		this.AttachDeveloperTools();
#endif
	}

	public override void OnFrameworkInitializationCompleted()
	{
		KyoshinEewViewerApp.Application = this;

		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			KyoshinEewViewerApp.Selector = ThemeSelector.Create(".");
			KyoshinEewViewerApp.Selector.EnableThemes(this);
			desktop.ShutdownMode = ShutdownMode.OnLastWindowClose;

			SplashWindow? splashWindow = null;
			if (!(StartupOptions.Current?.NoSplash ?? false))
			{
				splashWindow = new SplashWindow();
				desktop.MainWindow = splashWindow;
				splashWindow.Show();
			}

			var config = ServiceLocator.Current.RequireService<KyoshinEewViewerConfiguration>();
			var subWindow = ServiceLocator.Current.RequireService<ISubWindowsService>();

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
			KyoshinEewViewerApp.Selector.ObservePropertyChanged(x => x.SelectedIntensityTheme)
				.Subscribe(x =>
				{
					if (x == null) return;
					config.Theme.IntensityTheme = x.Meta;
					Dispatcher.UIThread.Post(() => FixedObjectRenderer.UpdateIntensityPaintCache(desktop.Windows[0]));
				});

			Task.Run(async () =>
			{
				try
				{
					// 多重起動警告
					if (StartupOptions.Current?.StandaloneSeriesName is null &&
#if INTEGRATION_TEST
						// 結合テスト時は既存インスタンスの検知で止まらないようにする
						StartupOptions.Current?.SmokeTest != true && StartupOptions.Current?.AutoUpdateTest != true &&
#endif
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
									splashWindow?.Close();
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
							if (splashWindow != null)
								dialog.Show(splashWindow);
							else
								dialog.Show();
						});
						mre.Wait();
						if (!dialog?.IsContinue ?? false)
						{
							await Dispatcher.UIThread.InvokeAsync(() =>
							{
								splashWindow?.Close();
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
							DataContext = ServiceLocator.Current.RequireService<MainViewModel>(),
						};
						desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
						KyoshinEewViewerApp.Selector.ObservePropertyChanged(x => x.SelectedWindowTheme).Where(x => x != null).Subscribe(x =>
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
							if (StartupOptions.Current?.StandaloneSeriesName is null
#if INTEGRATION_TEST
								// 結合テスト時は既存インスタンスとのパイプ名衝突を避けるため起動しない
								&& StartupOptions.Current?.SmokeTest != true && StartupOptions.Current?.AutoUpdateTest != true
#endif
							)
							{
								_ipcService = InterProcessCommunicationServiceFactory.Create();
								_ipcService.StartServer();
							}

#if INTEGRATION_TEST
							// スモークテストモード: メインウィンドウ表示を記録して自動終了する
							if (StartupOptions.Current?.SmokeTest == true)
							{
								IntegrationTestSentinel.Write("main-window-opened");
								_ = Task.Run(async () =>
								{
									await Task.Delay(3000);
									await Dispatcher.UIThread.InvokeAsync(() => desktop.Shutdown());
								});
							}
#endif
						};
						MainWindow.Show();
						MainWindow.Activate();
					});
				}
				catch (Exception ex)
				{
					await Dispatcher.UIThread.InvokeAsync(async () =>
					{
						if (splashWindow != null)
						{
							await new FAContentDialog
							{
								Title = "起動に失敗しました",
								Content = new SelectableTextBlock { Text = ex.ToString() },
								CloseButtonText = "OK"
							}.ShowAsync(splashWindow);

							splashWindow?.Close();
						}
						desktop.Shutdown();
					});
				}
			}).ConfigureAwait(false);

			desktop.Exit += (s, e) =>
			{
				StrongReferenceMessenger.Default.Send(new ApplicationClosing());
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
		var services = new ServiceCollection();
		services.SetupConfigurationAndLogging();
		services.AddKyoshinEewViewer();

		services.AddSingleton<ISubWindowsService, SubWindowsService>();

		// プラットフォーム依存の通知プロバイダを登録する。
		// 解決時 (NotificationService.Initialize) に生成し、AUMID 登録等が起動直後に走らないよう遅延させる。
		// 未対応 OS では null を返し、通知は無効になる
		services.AddSingleton(_ => CreateNotificationProvider()!);
		// トレイアイコンは Avalonia の TrayIcon で全デスクトップ共通に扱う
		services.AddSingleton<TrayIconProvider>(_ => new Notification.AvaloniaTrayIconProvider());

		ServiceLocator.SetProvider(services.BuildServiceProvider());
		base.RegisterServices();
	}

	private static NotificationProvider? CreateNotificationProvider()
	{
#if WINDOWS
		return new Notification.Windows.WindowsNotificationProvider();
#else
		if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			return new Notification.MacOS.MacOsNotificationProvider();
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			return new Notification.Linux.LinuxNotificationProvider();
		return null;
#endif
	}

	public void OpenSettingsClicked(object sender, EventArgs args)
		=> StrongReferenceMessenger.Default.Send(new ShowSettingWindowRequested());

}
