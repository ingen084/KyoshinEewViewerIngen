using Avalonia;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using CommandLine;
using KyoshinEewViewer.Core;
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
				if (StartupOptions.Current.CurrentDirectory is { } cd)
					Environment.CurrentDirectory = cd;
			});
		BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
	}

	// Avalonia configuration, don't remove; also used by visual designer.
	public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
		.UsePlatformDetect()
		.LogToTrace()
		.With(new Win32PlatformOptions
		{
			//CompositionMode = new[] { 
			//	Win32CompositionMode.LowLatencyDxgiSwapChain,
			//	Win32CompositionMode.WinUIComposition,
			//},
		})
		.With(new FontManagerOptions
		{
			DefaultFamilyName = "avares://KyoshinEewViewer.Core/Assets/Fonts/NotoSansJP/#Noto Sans JP",
			FontFallbacks = new[]
			{
				new FontFallback
				{
					FontFamily = new FontFamily("avares://KyoshinEewViewer.Core/Assets/Fonts/NotoSansJP/#Noto Sans JP"),
				},
				new FontFallback
				{
					FontFamily = new FontFamily(Utils.IconFontName),
				},
			},
		})
		.UseSkia()
		.UseReactiveUI();
}
