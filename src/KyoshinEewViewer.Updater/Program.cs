using Avalonia;

namespace KyoshinEewViewer.Updater;

static class Program
{
	public static string? OverrideKevPath { get; private set; }
	public static void Main(string[] args)
	{
		if (args.Length >= 1)
			OverrideKevPath = args[0];
		BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
	}

	// Avalonia configuration, don't remove; also used by visual designer.
	public static AppBuilder BuildAvaloniaApp()
		=> AppBuilder.Configure<App>()
			.UsePlatformDetect()
			.LogToTrace();
}
