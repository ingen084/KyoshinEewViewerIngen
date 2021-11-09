using Avalonia;
using Avalonia.Headless;
using Avalonia.ReactiveUI;
using System.Linq;

namespace EarthquakeRenderTest;

class Program
{
	// Initialization code. Don't use any Avalonia, third-party APIs or any
	// SynchronizationContext-reliant code before AppMain is called: things aren't initialized
	// yet and stuff might break.
	public static void Main(string[] args)
	{
		var builder = BuildAvaloniaApp(args);
		if (args.Any(a => a.ToLower() == "--headless"))
			builder.StartWithHeadlessVncPlatform("0.0.0.0", 14190, args);
		else
			builder.StartWithClassicDesktopLifetime(args);
	}

	// Avalonia configuration, don't remove; also used by visual designer.
	public static AppBuilder BuildAvaloniaApp(string[] args)
	{
		var builder = AppBuilder.Configure<App>()
			.UsePlatformDetect()
			.UseReactiveUI()
			.LogToTrace()
			.UseSkia();
		if (args.Any(a => a.ToLower() == "--headless"))
			return builder.UseHeadless();
		return builder;
	}
}
