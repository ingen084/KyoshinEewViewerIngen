using Avalonia;
using Avalonia.ReactiveUI;
using System;
using System.Runtime;

namespace KyoshinEewViewer;

internal class Program
{
	// Initialization code. Don't use any Avalonia, third-party APIs or any
	// SynchronizationContext-reliant code before AppMain is called: things aren't initialized
	// yet and stuff might break.
	public static void Main(string[] args)
	{
		if (args.Length == 2 && args[0] == "standalone")
		{
			StartupOptions.IsStandalone = true;
			StartupOptions.StandaloneSeriesName = args[1];
		}

		ProfileOptimization.SetProfileRoot(Environment.CurrentDirectory);
		ProfileOptimization.StartProfile("KyoshinEewViewer.jitprofile");
		BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
	}

	// Avalonia configuration, don't remove; also used by visual designer.
	public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
		.UsePlatformDetect()
		.LogToTrace()
		.With(new Win32PlatformOptions
		{
			UseWgl = true,
			AllowEglInitialization = true,
			EnableMultitouch = false,
		})
		.With(new X11PlatformOptions
		{
			OverlayPopups = true,
		})
		.UseSkia()
		.UseReactiveUI();
}
