using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.ViewModels;
using KyoshinEewViewer.Views;
using Microsoft.Extensions.Logging;
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
	public static MainWindow? MainWindow { get; private set; }

	public override void Initialize() => AvaloniaXamlLoader.Load(this);

	public override void OnFrameworkInitializationCompleted()
	{
		Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));
#if DEBUG
		Trace.Listeners.Add(new LoggingTraceListener());
#endif

		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			Selector = ThemeSelector.Create(".");
			Selector.EnableThemes(this);

			var splashWindow = new SplashWindow();
			splashWindow.Show();

			ConfigurationService.Load();

			Selector.ApplyTheme(ConfigurationService.Current.Theme.WindowThemeName, ConfigurationService.Current.Theme.IntensityThemeName);

			Task.Run(async () =>
			{
				if (!StartupOptions.IsStandalone && Process.GetProcessesByName("KyoshinEewViewer").Count() > 1)
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

				await Dispatcher.UIThread.InvokeAsync(() =>
				{
					desktop.MainWindow = MainWindow = new MainWindow
					{
						DataContext = new MainWindowViewModel(),
					};
					Selector.WhenAnyValue(x => x.SelectedIntensityTheme).Where(x => x != null)
						.Subscribe(x =>
						{
							ConfigurationService.Current.Theme.IntensityThemeName = x?.Name ?? "Standard";
							FixedObjectRenderer.UpdateIntensityPaintCache(desktop.MainWindow);
						});
					Selector.WhenAnyValue(x => x.SelectedWindowTheme).Where(x => x != null).Subscribe(x =>
					{
						ConfigurationService.Current.Theme.WindowThemeName = x?.Name ?? "Light";
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
					MainWindow.Show();
					MainWindow.Activate();
					splashWindow.Close();
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
		Locator.CurrentMutable.RegisterLazySingleton(() => new NotificationService(), typeof(NotificationService));
		Locator.CurrentMutable.RegisterLazySingleton(() => new TelegramProvideService(), typeof(TelegramProvideService));
		base.RegisterServices();
	}

	public class LoggingTraceListener : TraceListener
	{
		private Microsoft.Extensions.Logging.ILogger Logger { get; }

		public LoggingTraceListener()
		{
			Logger = LoggingService.CreateLogger(this);
		}

		public override void Write(string? message)
			=> Logger.LogTrace("writed: {message}", message);

		public override void WriteLine(string? message)
			=> Logger.LogTrace("line writed: {message}", message);
	}
}
