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
using KyoshinEewViewer.Map.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using System.Net;

namespace SlackBot
{
	internal class Program
	{
		public static AutoResetEvent Are { get; } = new(false);

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

			var webBuilder = WebApplication.CreateSlimBuilder(args);
			webBuilder.WebHost.ConfigureKestrel((context, serverOptions) =>
			{
				serverOptions.Listen(IPAddress.Loopback, 5000);
			});
			var webApp = webBuilder.Build();
			webApp.MapGet("/", async context =>
			{
				context.Response.ContentType = "image/webp";
				await window.CaptureImageAsync(context.Response.BodyWriter.AsStream());
			});

			Console.CancelKeyPress += (s, e) =>
			{
				tokenSource.Cancel();
				e.Cancel = true;
				logger.LogInfo("キャンセルキーを検知しました。");
				webApp.StopAsync().Wait();
				Dispatcher.UIThread.Invoke(() => window.Close());
				Dispatcher.UIThread.InvokeShutdown();
			};
			Dispatcher.UIThread.ShutdownStarted += (s, e) => logger.LogInfo("シャットダウンを開始しました。");
			Dispatcher.UIThread.ShutdownFinished += (s, e) => logger.LogInfo("シャットダウンが完了しました。");

			webApp.RunAsync();
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
