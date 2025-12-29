using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.ShakeDetection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ShakeDetectionProducer.Services;

namespace ShakeDetectionProducer;

internal class Program
{
	private static readonly ActivitySource ActivitySource = new("ShakeDetectionProducer");
	private static readonly Meter Meter = new("ShakeDetectionProducer");

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
	private static bool _isInitialized;

	// ObservableGaugeはMeterから作成
	private static readonly ObservableGauge<double> DelayGauge = Meter.CreateObservableGauge(
		"kyoshin_monitor.delay",
		() => _currentDelay,
		"ms",
		"Data delay from Kyoshin Monitor");

	public static async Task Main(string[] args)
	{
		CultureInfo.CurrentCulture = new CultureInfo("ja-JP");

		// WebアプリケーションのBuilderを作成
		var builder = WebApplication.CreateBuilder(args);

		// OpenTelemetry設定
		var serviceName = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME") ?? "shake-detection-producer";
		var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");

		builder.Services.AddOpenTelemetry()
			.ConfigureResource(resource => resource.AddService(serviceName))
			.WithTracing(tracing =>
			{
				tracing
					.AddSource("ShakeDetectionProducer")
					.AddSource("System.Net.Http")
					.AddAspNetCoreInstrumentation()
					.AddHttpClientInstrumentation();

				if (!string.IsNullOrEmpty(otlpEndpoint))
					tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
				else
					tracing.AddConsoleExporter();
			})
			.WithMetrics(metrics =>
			{
				metrics
					.AddMeter("ShakeDetectionProducer")
					.AddAspNetCoreInstrumentation()
					.AddHttpClientInstrumentation();

				if (!string.IsNullOrEmpty(otlpEndpoint))
					metrics.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
				else
					metrics.AddConsoleExporter();
			});

		// サービス登録
		builder.Services.AddSingleton<KafkaProducer>();
		builder.Services.AddSingleton<SimpleTimerService>();
		builder.Services.AddSingleton<SimpleImageFetcher>();
		builder.Services.AddSingleton<SimpleObservationPointsLoader>();
		builder.Services.AddSingleton<ShakeEventTracker>();
		builder.Services.AddSingleton<ShakeDetectionEngine>();

		// Kestrel設定
		var port = int.Parse(Environment.GetEnvironmentVariable("PORT") ?? "5000");
		builder.WebHost.ConfigureKestrel(serverOptions =>
		{
			serverOptions.Listen(IPAddress.Any, port);
		});

		var app = builder.Build();

		// サービス取得
		var logger = app.Services.GetRequiredService<ILogger<Program>>();
		var kafkaProducer = app.Services.GetRequiredService<KafkaProducer>();
		var timerService = app.Services.GetRequiredService<SimpleTimerService>();
		var imageFetcher = app.Services.GetRequiredService<SimpleImageFetcher>();
		var pointsLoader = app.Services.GetRequiredService<SimpleObservationPointsLoader>();
		var eventTracker = app.Services.GetRequiredService<ShakeEventTracker>();
		var shakeDetectionEngine = app.Services.GetRequiredService<ShakeDetectionEngine>();

		// ヘルスチェックエンドポイント
		app.MapGet("/health", () => Results.Ok(new
		{
			Status = _isInitialized ? "healthy" : "initializing",
			LastDataTime = _lastDataTime,
			DelayMs = _currentDelay
		}));

		// 観測点データの読み込み
		var observationPointsPath = Environment.GetEnvironmentVariable("OBSERVATION_POINTS_PATH");
		if (string.IsNullOrEmpty(observationPointsPath))
		{
			logger.LogError("環境変数 OBSERVATION_POINTS_PATH が設定されていません");
			return;
		}

		logger.LogInformation("観測点データを読み込みます: {Path}", observationPointsPath);

		try
		{
			var points = await pointsLoader.LoadAsync(observationPointsPath);
			var realtimePoints = points
				.Where(p => p is { Point: not null, IsSuspended: false })
				.Select(p => new RealtimeObservationPoint(p))
				.ToArray();

			shakeDetectionEngine.Initialize(realtimePoints);
			logger.LogInformation("ShakeDetectionEngine を初期化しました: {Count}観測点", realtimePoints.Length);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "観測点データの読み込みに失敗しました");
			return;
		}

		// 画像取得エラー時の処理
		imageFetcher.ErrorOccurred += async message =>
		{
			if (message.Contains("タイムアウト"))
			{
				TimeoutCounter.Add(1);
				try
				{
					var payload = ErrorPayload.Timeout(DateTime.Now);
					await kafkaProducer.ProduceErrorAsync(payload);
				}
				catch (Exception ex)
				{
					logger.LogError(ex, "タイムアウトエラーのKafka送信に失敗しました");
				}
			}
			else if (message.Contains("エラー") || message.Contains("失敗"))
			{
				ErrorCounter.Add(1);
				try
				{
					var payload = ErrorPayload.HttpError(DateTime.Now, message);
					await kafkaProducer.ProduceErrorAsync(payload);
				}
				catch (Exception ex)
				{
					logger.LogError(ex, "HTTPエラーのKafka送信に失敗しました");
				}
			}
		};

		// タイマーイベント処理
		timerService.TimerElapsed += async time =>
		{
			using var activity = ActivitySource.StartActivity("kyoshin_monitor.process");
			activity?.SetTag("time", time.ToString("yyyy-MM-dd HH:mm:ss"));

			var fetchStopwatch = Stopwatch.StartNew();

			// 画像取得
			var bitmap = await imageFetcher.FetchImageAsync(time);

			fetchStopwatch.Stop();
			FetchDurationHistogram.Record(fetchStopwatch.Elapsed.TotalMilliseconds);

			if (bitmap == null)
				return;

			var processStopwatch = Stopwatch.StartNew();

			try
			{
				// 揺れ検知処理
				using (bitmap)
					shakeDetectionEngine.ProcessImage(bitmap, time);

				// 確定済みイベントの処理
				var confirmedEvents = shakeDetectionEngine.KyoshinEvents
					.Where(e => e.IsConfirmed)
					.ToArray();

				foreach (var evt in confirmedEvents)
				{
					var (shouldSend, isLevelUp) = eventTracker.ProcessEvent(evt);
					if (!shouldSend)
						continue;

					using var sendActivity = ActivitySource.StartActivity("shake_detected.send");
					sendActivity?.SetTag("event.id", evt.Id.ToString());
					sendActivity?.SetTag("event.level", evt.Level.ToString());
					sendActivity?.SetTag("event.is_level_up", isLevelUp);

					var payload = ShakeDetectedPayload.FromEvent(evt, isLevelUp, false);

					try
					{
						await kafkaProducer.ProduceShakeDetectedAsync(payload);
						logger.LogInformation("揺れ検知イベントを送信しました: {EventId} Level={Level} IsLevelUp={IsLevelUp}",
							evt.Id, evt.Level, isLevelUp);
					}
					catch (Exception ex)
					{
						logger.LogError(ex, "揺れ検知イベントのKafka送信に失敗しました: {EventId}", evt.Id);
						sendActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
					}
				}

				eventTracker.CleanupCache(confirmedEvents);

				// メトリクス更新
				_lastDataTime = time;
				_currentDelay = (DateTime.Now - time).TotalMilliseconds;
			}
			finally
			{
				processStopwatch.Stop();
				ProcessDurationHistogram.Record(processStopwatch.Elapsed.TotalMilliseconds);
			}
		};

		// シャットダウン処理
		Console.CancelKeyPress += (_, e) =>
		{
			e.Cancel = true;
			logger.LogInformation("シャットダウンを開始します...");
			app.StopAsync().Wait();
		};

		// 起動ログ
		var kafkaServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS") ?? "localhost:9092";
		var kafkaTopic = Environment.GetEnvironmentVariable("KAFKA_TOPIC") ?? "shake-detect-events";
		logger.LogInformation("ShakeDetectionProducer を起動します");
		logger.LogInformation("Kafka: {Servers}, Topic={Topic}", kafkaServers, kafkaTopic);
		logger.LogInformation("ヘルスチェック: http://0.0.0.0:{Port}/health", port);

		// タイマー開始
		var timerOffset = int.Parse(Environment.GetEnvironmentVariable("TIMER_OFFSET_MS") ?? "1100");
		timerService.Start(timerOffset);
		_isInitialized = true;

		// アプリケーション実行
		try
		{
			await app.RunAsync();
		}
		catch (OperationCanceledException)
		{
			// シャットダウン時は正常終了扱い
		}

		// クリーンアップ
		logger.LogInformation("クリーンアップを実行しています...");
		kafkaProducer.Flush(TimeSpan.FromSeconds(5));
		kafkaProducer.Dispose();
		timerService.Dispose();
		imageFetcher.Dispose();

		logger.LogInformation("シャットダウンが完了しました");
	}
}
