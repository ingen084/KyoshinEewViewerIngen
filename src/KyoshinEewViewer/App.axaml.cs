using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
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
using System.Reactive.Linq;
using System.Runtime.InteropServices;

namespace KyoshinEewViewer;

public class App : Application
{
	[DllImport("dwmapi.dll", PreserveSig = true)]
	private static extern int DwmSetWindowAttribute(IntPtr hwnd, DWMWINDOWATTRIBUTE attr, ref int attrValue, int attrSize);
	private enum DWMWINDOWATTRIBUTE
	{
		DWMWA_NCRENDERING_ENABLED,
		DWMWA_NCRENDERING_POLICY,
		DWMWA_TRANSITIONS_FORCEDISABLED,
		DWMWA_ALLOW_NCPAINT,
		DWMWA_CAPTION_BUTTON_BOUNDS,
		DWMWA_NONCLIENT_RTL_LAYOUT,
		DWMWA_FORCE_ICONIC_REPRESENTATION,
		DWMWA_FLIP3D_POLICY,
		DWMWA_EXTENDED_FRAME_BOUNDS,
		DWMWA_HAS_ICONIC_BITMAP,
		DWMWA_DISALLOW_PEEK,
		DWMWA_EXCLUDED_FROM_PEEK,
		DWMWA_CLOAK,
		DWMWA_CLOAKED,
		DWMWA_FREEZE_REPRESENTATION,
		DWMWA_PASSIVE_UPDATE_MODE,
		DWMWA_USE_HOSTBACKDROPBRUSH,
		DWMWA_USE_IMMERSIVE_DARK_MODE = 20,
		DWMWA_WINDOW_CORNER_PREFERENCE = 33,
		DWMWA_BORDER_COLOR,
		DWMWA_CAPTION_COLOR,
		DWMWA_TEXT_COLOR,
		DWMWA_VISIBLE_FRAME_BORDER_THICKNESS,
		DWMWA_LAST
	};

	public static ThemeSelector? Selector { get; private set; }
	public static MainWindow? MainWindow { get; private set; }

	public override void Initialize() => AvaloniaXamlLoader.Load(this);

	public override void OnFrameworkInitializationCompleted()
	{
		Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));

		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			Selector = ThemeSelector.Create(".");
			Selector.EnableThemes(this);
			ConfigurationService.Load();

#if DEBUG
			Trace.Listeners.Add(new LoggingTraceListener());
#endif

			Selector.ApplyTheme(ConfigurationService.Current.Theme.WindowThemeName, ConfigurationService.Current.Theme.IntensityThemeName);

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

					var color = FindColorResource("DockTitleBackgroundColor");
					var colord = color.R | color.G << 8 | color.B << 16;
					DwmSetWindowAttribute(
						desktop.MainWindow.PlatformImpl.Handle.Handle,
						DWMWINDOWATTRIBUTE.DWMWA_CAPTION_COLOR,
						ref colord,
						Marshal.SizeOf(colord));

					//var color2 = FindColorResource("SubForegroundColor");
					//var colord2 = color2.R | color2.G << 8 | color2.B << 16;
					//DwmSetWindowAttribute(
					//	desktop.MainWindow.PlatformImpl.Handle.Handle,
					//	DWMWINDOWATTRIBUTE.DWMWA_BORDER_COLOR,
					//	ref colord2,
					//	Marshal.SizeOf(colord2));
				}
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
