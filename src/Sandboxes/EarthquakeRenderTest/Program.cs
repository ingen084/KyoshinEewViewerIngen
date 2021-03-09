using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.ReactiveUI;
using System;
using System.Linq;

namespace EarthquakeRenderTest
{
	class Program
	{
		// Initialization code. Don't use any Avalonia, third-party APIs or any
		// SynchronizationContext-reliant code before AppMain is called: things aren't initialized
		// yet and stuff might break.
		public static void Main(string[] args) => BuildAvaloniaApp(args)
			.StartWithClassicDesktopLifetime(args);

		// Avalonia configuration, don't remove; also used by visual designer.
		public static AppBuilder BuildAvaloniaApp(string[] args)
		{
			var builder = AppBuilder.Configure<App>()
				.UsePlatformDetect()
				.LogToTrace()
				.UseSkia()
				.UseReactiveUI();
			if (args.Any(a => a.ToLower() == "--headless"))
				return builder.UseHeadless();
			return builder;
		}
	}
}
