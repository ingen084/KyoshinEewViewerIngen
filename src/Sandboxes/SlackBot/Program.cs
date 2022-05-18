using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.ReactiveUI;
using KyoshinEewViewer.Services;
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
			ConfigurationService.Load();
			// 強制設定
			ConfigurationService.Current.Logging.Enable = true;
			ConfigurationService.Current.Map.AutoFocusAnimation = false;
			ConfigurationService.Current.Update.SendCrashReport = false;
			ConfigurationService.Current.KyoshinMonitor.UseExperimentalShakeDetect = true;
			LoggingService.EnableConsoleLogger = true;
			var builder = BuildAvaloniaApp();
#if !DEBUG
			builder.UseHeadless();
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
