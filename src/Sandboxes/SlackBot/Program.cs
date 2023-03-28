using Avalonia;
using Avalonia.ReactiveUI;
using KyoshinEewViewer;
using System;

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
            ConfigurationLoader.Load();
            // 強制設定
            ConfigurationLoader.Current.Logging.Enable = true;
            ConfigurationLoader.Current.Map.AutoFocusAnimation = false;
            ConfigurationLoader.Current.Update.SendCrashReport = false;
            ConfigurationLoader.Current.KyoshinMonitor.UseExperimentalShakeDetect = true;
            LoggingAdapter.EnableConsoleLogger = true;
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
