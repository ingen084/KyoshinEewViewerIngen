using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Net;
using System.Threading;
using Avalonia;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using Avalonia.Headless;
using Avalonia.Threading;
using KyoshinEewViewer;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Map.Data;
using KyoshinEewViewer.Series.KyoshinMonitor;
using KyoshinEewViewer.Series.KyoshinMonitor.Events;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ReactiveUI;
using Splat;

namespace ShakeDetectWebSocketServer;

internal class Program
{
	private static readonly ActivitySource ActivitySource = new("ShakeDetectWebSocketServer");
	private static readonly Meter Meter = new("ShakeDetectWebSocketServer");

	// 強震モニタメトリクス
	private static readonly Histogram<double> FetchDurationHistogram = Meter.CreateHistogram<double>(
		"kyoshin_monitor.fetch_duration",
		"ms",
		"Time to fetch image from Kyoshin Monitor");

	private static readonly Histogram<double> ProcessDurationHistogram = Meter.CreateHistogram<double>(
		"kyoshin_monitor.process_duration",
		"ms",
		"Time to process shake detection");

	private static readonly Counter<long> TimeoutCounter = Meter.CreateCounter<long>(
		"kyoshin_monitor.timeout_count",
		"times",
		"Number of timeouts when fetching from Kyoshin Monitor");

	private static readonly Counter<long> ErrorCounter = Meter.CreateCounter<long>(
		"kyoshin_monitor.error_count",
		"times",
		"Number of errors when fetching from Kyoshin Monitor");

	private static double _currentDelay;
	private static DateTime _lastDataTime = DateTime.MinValue;

	// ObservableGaugeはMeterから作成
	private static readonly ObservableGauge<double> DelayGauge = Meter.CreateObservableGauge(
		"kyoshin_monitor.delay",
		() => _currentDelay,
		"ms",
		"Data delay from Kyoshin Monitor");

	[STAThread]
	public static void Main(string[] args)
	{
		CultureInfo.CurrentCulture = new CultureInfo("ja-JP");
		LoggingAdapter.EnableConsoleLogger = true;
		PolygonFeature.AsyncVerticeMode = false;
		PolylineFeature.AsyncMode = false;

		var builder = BuildAvaloniaApp();
		builder.SetupWithoutStarting();

		var logger = Locator.Current.RequireService<ILogManager>().GetLogger<Program>();

		// WebアプリケーションのBuilderを作成
		var webBuilder = WebApplication.CreateBuilder(args);

		// OpenTelemetry設定
		var serviceName = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME") ?? "shake-detect-websocket-server";
		var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");

		webBuilder.Services.AddOpenTelemetry()
			.ConfigureResource(resource => resource.AddService(serviceName))
			.WithTracing(tracing =>
			{
				tracing
					.AddSource("ShakeDetectWebSocketServer")
					.AddSource("System.Net.Http")
					.AddAspNetCoreInstrumentation()
					.AddHttpClientInstrumentation();

				if (!string.IsNullOrEmpty(otlpEndpoint))
				{
					tracing.AddOtlpExporter(options =>
					{
						options.Endpoint = new Uri(otlpEndpoint);
					});
				}
				else
				{
					tracing.AddConsoleExporter();
				}
			})
			.WithMetrics(metrics =>
			{
				metrics
					.AddMeter("ShakeDetectWebSocketServer")
					.AddAspNetCoreInstrumentation()
					.AddHttpClientInstrumentation();

				if (!string.IsNullOrEmpty(otlpEndpoint))
				{
					metrics.AddOtlpExporter(options =>
					{
						options.Endpoint = new Uri(otlpEndpoint);
					});
				}
				else
				{
					metrics.AddConsoleExporter();
				}
			});

		// WebSocketHandler登録
		webBuilder.Services.AddSingleton<WebSocketHandler>();

		// Kestrel設定
		var port = int.Parse(Environment.GetEnvironmentVariable("PORT") ?? "5000");
		webBuilder.WebHost.ConfigureKestrel(serverOptions =>
		{
			serverOptions.Listen(IPAddress.Any, port);
		});

		var webApp = webBuilder.Build();

		// WebSocket設定
		webApp.UseWebSockets(new WebSocketOptions
		{
			KeepAliveInterval = TimeSpan.FromSeconds(30)
		});

		var webSocketHandler = webApp.Services.GetRequiredService<WebSocketHandler>();

		// WebSocketエンドポイント
		webApp.Map("/ws", async context =>
		{
			if (context.WebSockets.IsWebSocketRequest)
			{
				using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
				await webSocketHandler.HandleConnectionAsync(webSocket, context.RequestAborted);
			}
			else
			{
				context.Response.StatusCode = StatusCodes.Status400BadRequest;
			}
		});

		// ヘルスチェックエンドポイント
		webApp.MapGet("/health", () => Results.Ok(new
		{
			Status = "healthy",
			Connections = webSocketHandler.ConnectionCount,
			LastDataTime = _lastDataTime,
			DelayMs = _currentDelay
		}));

		// 強震モニタサービスを取得してイベント購読
		var kyoshinMonitorSeries = Locator.Current.GetService<KyoshinMonitorSeries>();
		if (kyoshinMonitorSeries == null)
		{
			logger.LogError("KyoshinMonitorSeriesが取得できませんでした");
			return;
		}

		// 揺れ検知イベントを購読
		MessageBus.Current.Listen<KyoshinShakeDetected>().Subscribe(async x =>
		{
			using var activity = ActivitySource.StartActivity("shake_detected.process");
			activity?.SetTag("event.id", x.Event.Id);
			activity?.SetTag("event.level", x.Event.Level.ToString());
			activity?.SetTag("event.is_level_up", x.IsLevelUp);
			activity?.SetTag("event.is_replay", x.IsReplay);

			var payload = ShakeDetectedPayload.FromEvent(x.Event, x.IsLevelUp, x.IsReplay);

			try
			{
				await webSocketHandler.BroadcastAsync(payload);
				logger.LogInfo($"揺れ検知イベントを送信しました: {x.Event.Id} Level={x.Event.Level}");
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "揺れ検知イベントの送信に失敗しました");
				activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			}
		});

		// 強震モニタのメトリクス用イベント購読
		var kyoshinMonitorWatchService = Locator.Current.GetService<KyoshinEewViewer.Series.KyoshinMonitor.Services.KyoshinMonitorWatchService>();
		if (kyoshinMonitorWatchService != null)
		{
			kyoshinMonitorWatchService.RealtimeDataUpdated += data =>
			{
				_lastDataTime = data.time;
				_currentDelay = (DateTime.Now - data.time).TotalMilliseconds;
			};

			kyoshinMonitorWatchService.WarningMessageUpdated += async message =>
			{
				// タイムアウトやエラーを検出してクライアントに通知
				if (message.Contains("タイムアウト"))
				{
					TimeoutCounter.Add(1);
					var payload = ErrorPayload.Timeout(DateTime.Now);
					await webSocketHandler.BroadcastAsync(payload);
				}
				else if (message.Contains("エラー"))
				{
					ErrorCounter.Add(1, new KeyValuePair<string, object?>("error_type", "general"));
					var payload = ErrorPayload.HttpError(DateTime.Now, message);
					await webSocketHandler.BroadcastAsync(payload);
				}
			};
		}

		// シャットダウン処理
		Console.CancelKeyPress += async (s, e) =>
		{
			e.Cancel = true;
			logger.LogInfo("シャットダウンを開始します...");

			await webSocketHandler.CloseAllAsync();
			await webApp.StopAsync();

			Dispatcher.UIThread.InvokeShutdown();
		};

		Dispatcher.UIThread.ShutdownStarted += (s, e) => logger.LogInfo("UIスレッドのシャットダウンを開始しました。");
		Dispatcher.UIThread.ShutdownFinished += (s, e) => logger.LogInfo("UIスレッドのシャットダウンが完了しました。");

		logger.LogInfo($"WebSocketサーバーを起動します: http://0.0.0.0:{port}/ws");

		// WebアプリケーションとAvaloniaを並行実行
		webApp.RunAsync();
		Dispatcher.UIThread.MainLoop(CancellationToken.None);
	}

	public static AppBuilder BuildAvaloniaApp()
		=> AppBuilder.Configure<App>()
			.UseSkia()
			.UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
			.LogToTrace()
			.UseReactiveUI();
}
