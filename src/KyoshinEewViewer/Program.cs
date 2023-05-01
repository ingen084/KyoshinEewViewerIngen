using Avalonia;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using CommandLine;
using System;
using System.Globalization;

namespace KyoshinEewViewer;

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
				if (StartupOptions.Current.CurrentDirectory is string cd)
					Environment.CurrentDirectory = cd;
			});
		BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
	}

	// Avalonia configuration, don't remove; also used by visual designer.
	public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
		.UsePlatformDetect()
		.LogToTrace()
		//.With(new AvaloniaNativePlatformOptions
		//{
		//	UseDeferredRendering = false,
		//	UseCompositor = false,
		//})
		.With(new Win32PlatformOptions
		{
			AllowEglInitialization = true,
			UseWindowsUIComposition = true,
			//UseLowLatencyDxgiSwapChain = true,
			//UseWgl = true,
		})
		.With(new X11PlatformOptions
		{
			//OverlayPopups = true,
		})
		.With(new FontManagerOptions
		{
			DefaultFamilyName = "avares://KyoshinEewViewer.Core/Assets/Fonts/NotoSansJP-Regular.otf#Noto Sans JP"
		})
		.UseSkia()
		.UseReactiveUI();
}
