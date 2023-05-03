using Avalonia;
using Avalonia.ReactiveUI;
#if !DEBUG
using Avalonia.Headless;
#endif
using KyoshinEewViewer;
using System;
using System.Globalization;

namespace SlackBot
{
	internal class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
			CultureInfo.CurrentCulture = new CultureInfo("ja-JP");
			ConfigurationLoader.Load();
            var builder = BuildAvaloniaApp();
#if !DEBUG
			builder.UseHeadless(new() { UseHeadlessDrawing = false });
#endif
            builder.StartWithClassicDesktopLifetime(args);
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .UseSkia()
                .LogToTrace()
                .UseReactiveUI();
    }
}
