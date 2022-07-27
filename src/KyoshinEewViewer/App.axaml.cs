using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Rendering;
using Avalonia.Threading;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.CustomControl;
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
using static KyoshinEewViewer.NativeMethods;

namespace KyoshinEewViewer;

public class App : Application
{
	public static ThemeSelector? Selector { get; private set; }
	public static Window? MainWindow { get; set; }

	public override void Initialize() => AvaloniaXamlLoader.Load(this);

	public override void OnFrameworkInitializationCompleted()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			Selector = ThemeSelector.Create(".");
			Selector.EnableThemes(this);

			var splashWindow = new SplashWindow();
			splashWindow.Show();

			ConfigurationService.Load();

			Selector.ApplyTheme(ConfigurationService.Current.Theme.WindowThemeName, ConfigurationService.Current.Theme.IntensityThemeName);
			Selector.WhenAnyValue(x => x.SelectedIntensityTheme).Where(x => x != null)
				.Subscribe(x =>
				{
					ConfigurationService.Current.Theme.IntensityThemeName = x?.Name ?? "Standard";
					FixedObjectRenderer.UpdateIntensityPaintCache(desktop.Windows.First());
				});

			Task.Run(async () =>
			{
				if (!StartupOptions.IsStandalone && Process.GetProcessesByName("KyoshinEewViewer").Length > 1)
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
					ConfigurationService.Current.ShowWizard &&
					!StartupOptions.IsStandalone
				)
				{
					await Dispatcher.UIThread.InvokeAsync(async () =>
					{
						await SubWindowsService.Default.ShowDialogSetupWizardWindow(async () =>
						{
							await Task.Delay(500);
							await Dispatcher.UIThread.InvokeAsync(() =>
							{
								splashWindow?.Close();
								splashWindow = null;
							});
						});
					});
					ConfigurationService.Current.ShowWizard = false;
					ConfigurationService.Save();
				}

				await Dispatcher.UIThread.InvokeAsync(() =>
				{
					desktop.MainWindow = MainWindow = new MainWindow
					{
						DataContext = new MainWindowViewModel(),
					};
					Selector.WhenAnyValue(x => x.SelectedWindowTheme).Where(x => x != null).Subscribe(x =>
					{
						ConfigurationService.Current.Theme.WindowThemeName = x?.Name ?? "Light";
						FixedObjectRenderer.UpdateIntensityPaintCache(desktop.MainWindow);
						if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && desktop.MainWindow.PlatformImpl is not null)
						{
							Avalonia.Media.Color FindColorResource(string name)
								=> (Avalonia.Media.Color)(desktop.MainWindow.FindResource(name) ?? throw new Exception($"リソース {name} が見つかりませんでした"));
							bool FindBoolResource(string name)
								=> (bool)(desktop.MainWindow.FindResource(name) ?? throw new Exception($"リソース {name} が見つかりませんでした"));

							var isDarkTheme = FindBoolResource("IsDarkTheme");
							var USE_DARK_MODE = isDarkTheme ? 1 : 0;
							DwmSetWindowAttribute(
								desktop.MainWindow.PlatformImpl.Handle.Handle,
								DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE,
								ref USE_DARK_MODE,
								Marshal.SizeOf(USE_DARK_MODE));

							var color = FindColorResource("TitleBackgroundColor");
							var colord = color.R | color.G << 8 | color.B << 16;
							DwmSetWindowAttribute(
								desktop.MainWindow.PlatformImpl.Handle.Handle,
								DWMWINDOWATTRIBUTE.DWMWA_CAPTION_COLOR,
								ref colord,
								Marshal.SizeOf(colord));
						}
					});
					MainWindow.Opened += async (s, e) =>
					{
						await Task.Delay(1000);
						SubWindowsService.Default.SetupWizardWindow?.Close();
						splashWindow?.Close();
						splashWindow = null;
					};
					MainWindow.Show();
					MainWindow.Activate();
				});
			});

			desktop.Exit += (s, e) =>
			{
				MessageBus.Current.SendMessage(new ApplicationClosing());
				ConfigurationService.Save();
			};
		}

		base.OnFrameworkInitializationCompleted();
	}

	/// <summary>
	/// override RegisterServices register custom service
	/// </summary>
	public override void RegisterServices()
	{
		AvaloniaLocator.CurrentMutable.Bind<IFontManagerImpl>().ToConstant(new CustomFontManagerImpl());
		var timer = AvaloniaLocator.CurrentMutable.GetService<IRenderTimer>() ?? throw new Exception("RenderTimer が取得できません");
		AvaloniaLocator.CurrentMutable.Bind<IRenderTimer>().ToConstant(new FrameSkippableRenderTimer(timer));
		Locator.CurrentMutable.RegisterLazySingleton(() => new NotificationService(), typeof(NotificationService));
		Locator.CurrentMutable.RegisterLazySingleton(() => new TelegramProvideService(), typeof(TelegramProvideService));
		base.RegisterServices();
	}
}

public class FrameSkippableRenderTimer : IRenderTimer
{
	private IRenderTimer ParentTimer { get; }
	private ulong FrameCount { get; set; }

	public bool RunsInBackground => ParentTimer.RunsInBackground;

	public event Action<TimeSpan>? Tick;

	public FrameSkippableRenderTimer(IRenderTimer parentTimer)
	{
		ParentTimer = parentTimer;
		ParentTimer.Tick += t =>
		{
			if (ConfigurationService.Current.FrameSkip <= 1 || FrameCount++ % ConfigurationService.Current.FrameSkip == 0)
				Tick?.Invoke(t);
		};
	}
}
