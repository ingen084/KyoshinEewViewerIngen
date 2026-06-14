using Avalonia;
using CommandLine;
using KyoshinEewViewer;
using KyoshinEewViewer.Core;
using ReactiveUI.Avalonia;
using System;
using System.Globalization;

namespace KyoshinEewViewer.Desktop;

internal static class Program
{
	// Initialization code. Don't use any Avalonia, third-party APIs or any
	// SynchronizationContext-reliant code before AppMain is called: things aren't initialized
	// yet and stuff might break.
	[STAThread]
	public static void Main(string[] args)
	{
		CultureInfo.CurrentCulture = new CultureInfo("ja-JP");

		Parser.Default.ParseArguments<StartupOptions>(args)
			.WithParsed(o =>
			{
				StartupOptions.Current = o;
				if (StartupOptions.Current.CurrentDirectory is { } cd)
					Environment.CurrentDirectory = cd;
				if (o.ConsoleLog)
					LoggingAdapter.EnableConsoleLogger = true;
				if (o.DebugLog)
					LoggingAdapter.EnableDebugLog = true;
			});
		BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
	}

	// Avalonia configuration, don't remove; also used by visual designer.
	public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
		.UsePlatformDetect()
		.With(new X11PlatformOptions
		{
			OverlayPopups = true,
		})
		.With(new MacOSPlatformOptions
		{
			DisableAvaloniaAppDelegate = true
		})
		.LogToTrace(Avalonia.Logging.LogEventLevel.Error)
		.UseKeviFonts()
		.UseSkia()
		.UseHarfBuzz()
		.UseReactiveUI(_ => { });
}
