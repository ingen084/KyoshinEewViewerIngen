using Avalonia;
using Avalonia.ReactiveUI;
using Avalonia.Headless;
using Avalonia.Threading;
using KyoshinEewViewer;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series;
using Splat;
using System;
using System.Globalization;
using System.Threading;

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
			LoggingAdapter.EnableConsoleLogger = true;

			var builder = BuildAvaloniaApp();
			builder.UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
            builder.SetupWithoutStarting();

            var tokenSource = new CancellationTokenSource();
            
            var window = new MainWindow();
			window.Show();
			
            Console.CancelKeyPress += (s, e) =>
            {
	            e.Cancel = true;
	            Locator.Current.RequireService<ILogManager>().GetLogger<App>().LogInfo("キャンセルキーを検知しました。");
	            Dispatcher.UIThread.Invoke(() => window.Close());
	            Dispatcher.UIThread.InvokeShutdown();
			};
            Dispatcher.UIThread.ShutdownStarted += (s, e) => Console.WriteLine("シャットダウンを開始しました。");
            Dispatcher.UIThread.ShutdownFinished += (s, e) => Console.WriteLine("シャットダウンが完了しました。");
	        Dispatcher.UIThread.MainLoop(tokenSource.Token);
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
