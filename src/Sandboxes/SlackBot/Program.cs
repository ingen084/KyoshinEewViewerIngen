using Avalonia;
using Avalonia.ReactiveUI;
using Avalonia.Headless;
using Avalonia.Threading;
using KyoshinEewViewer;
using KyoshinEewViewer.Core;
using Splat;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using KyoshinEewViewer.Map.Data;

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
			PolygonFeature.VerticeMode = false;

			var builder = BuildAvaloniaApp();
			builder.SetupWithoutStarting();

			var tokenSource = new CancellationTokenSource();

			var window = new MainWindow();
			window.Show();

			var logger = Locator.Current.RequireService<ILogManager>().GetLogger<Program>();

			Console.CancelKeyPress += (s, e) =>
			{
				e.Cancel = true;
				logger.LogInfo("キャンセルキーを検知しました。");
				Dispatcher.UIThread.Invoke(() => window.Close());
				Dispatcher.UIThread.InvokeShutdown();
			};
			Dispatcher.UIThread.ShutdownStarted += (s, e) => logger.LogInfo("シャットダウンを開始しました。");
			Dispatcher.UIThread.ShutdownFinished += (s, e) => logger.LogInfo("シャットダウンが完了しました。");
			Task.Run(() =>
			{
				try
				{
					// CPU使用率を抑えるために優先度を低くした状態で10msごとに待機
					while (!tokenSource.IsCancellationRequested)
						Dispatcher.UIThread.Invoke(() => Thread.Sleep(10), DispatcherPriority.SystemIdle);
				}
				catch (TaskCanceledException) { }
			});
			Dispatcher.UIThread.MainLoop(tokenSource.Token);
		}

		// Avalonia configuration, don't remove; also used by visual designer.
		public static AppBuilder BuildAvaloniaApp()
			=> AppBuilder.Configure<App>()
				// .UsePlatformDetect()
				.UseSkia()
				.UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
				.LogToTrace()
				.UseReactiveUI();
	}
}
