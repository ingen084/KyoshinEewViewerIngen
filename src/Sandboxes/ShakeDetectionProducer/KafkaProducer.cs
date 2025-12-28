using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace ShakeDetectionProducer;

/// <summary>
/// Kafkaへのイベント送信を行うProducer
/// </summary>
public sealed class KafkaProducer : IDisposable
{
	private static readonly ActivitySource ActivitySource = new("ShakeDetectionProducer");
	private static readonly Meter Meter = new("ShakeDetectionProducer");

	private static readonly Counter<long> MessagesSentCounter = Meter.CreateCounter<long>(
		"kafka.messages_sent",
		"messages",
		"Total number of messages sent to Kafka");

	private static readonly Counter<long> MessagesFailedCounter = Meter.CreateCounter<long>(
		"kafka.messages_failed",
		"messages",
		"Total number of failed message deliveries");

	private static readonly Histogram<double> ProduceDurationHistogram = Meter.CreateHistogram<double>(
		"kafka.produce_duration",
		"ms",
		"Time to produce message to Kafka");

	private readonly IProducer<string, string> _producer;
	private readonly string _topic;
	private readonly ILogger<KafkaProducer> _logger;

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
	};

	public KafkaProducer(ILogger<KafkaProducer> logger)
	{
		_logger = logger;

		var bootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS") ?? "localhost:9092";
		_topic = Environment.GetEnvironmentVariable("KAFKA_TOPIC") ?? "shake-detect-events";

		var config = new ProducerConfig
		{
			BootstrapServers = bootstrapServers,
			ClientId = "shake-detection-producer",
			Acks = Acks.Leader,
			EnableIdempotence = false,
			MessageTimeoutMs = 5000,
			RequestTimeoutMs = 5000
		};

		_producer = new ProducerBuilder<string, string>(config)
			.SetErrorHandler((_, e) => _logger.LogError("Kafkaエラー: {Reason}", e.Reason))
			.SetLogHandler((_, log) =>
			{
				var level = log.Level switch
				{
					SyslogLevel.Emergency or SyslogLevel.Alert or SyslogLevel.Critical => LogLevel.Critical,
					SyslogLevel.Error => LogLevel.Error,
					SyslogLevel.Warning => LogLevel.Warning,
					SyslogLevel.Notice or SyslogLevel.Info => LogLevel.Information,
					_ => LogLevel.Debug
				};
				_logger.Log(level, "Kafka: {Message}", log.Message);
			})
			.Build();

		_logger.LogInformation("Kafka Producerを初期化しました: {BootstrapServers}, Topic={Topic}", bootstrapServers, _topic);
	}

	/// <summary>
	/// 揺れ検知イベントをKafkaに送信する
	/// </summary>
	public async Task ProduceShakeDetectedAsync(ShakeDetectedPayload payload, CancellationToken cancellationToken = default)
	{
		using var activity = ActivitySource.StartActivity("kafka.produce.shake_detected");
		activity?.SetTag("event.id", payload.EventId.ToString());
		activity?.SetTag("event.level", payload.Level);

		var stopwatch = Stopwatch.StartNew();

		try
		{
			var json = JsonSerializer.Serialize<KafkaPayload>(payload, JsonOptions);
			var message = new Message<string, string>
			{
				Key = payload.EventId.ToString(),
				Value = json
			};

			var result = await _producer.ProduceAsync(_topic, message, cancellationToken);

			stopwatch.Stop();
			ProduceDurationHistogram.Record(stopwatch.Elapsed.TotalMilliseconds);
			MessagesSentCounter.Add(1);

			activity?.SetTag("kafka.partition", result.Partition.Value);
			activity?.SetTag("kafka.offset", result.Offset.Value);

			_logger.LogDebug("Kafkaにメッセージを送信しました: Partition={Partition}, Offset={Offset}",
				result.Partition.Value, result.Offset.Value);
		}
		catch (ProduceException<string, string> ex)
		{
			stopwatch.Stop();
			MessagesFailedCounter.Add(1);
			activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			_logger.LogError(ex, "Kafkaへのメッセージ送信に失敗しました: {EventId}", payload.EventId);
			throw;
		}
	}

	/// <summary>
	/// エラーイベントをKafkaに送信する
	/// </summary>
	public async Task ProduceErrorAsync(ErrorPayload payload, CancellationToken cancellationToken = default)
	{
		using var activity = ActivitySource.StartActivity("kafka.produce.error");
		activity?.SetTag("error.type", payload.ErrorType);

		var stopwatch = Stopwatch.StartNew();

		try
		{
			var json = JsonSerializer.Serialize<KafkaPayload>(payload, JsonOptions);
			var message = new Message<string, string>
			{
				Key = $"error-{payload.Time:yyyyMMddHHmmss}",
				Value = json
			};

			var result = await _producer.ProduceAsync(_topic, message, cancellationToken);

			stopwatch.Stop();
			ProduceDurationHistogram.Record(stopwatch.Elapsed.TotalMilliseconds);
			MessagesSentCounter.Add(1);

			_logger.LogDebug("エラーイベントをKafkaに送信しました: Partition={Partition}, Offset={Offset}",
				result.Partition.Value, result.Offset.Value);
		}
		catch (ProduceException<string, string> ex)
		{
			stopwatch.Stop();
			MessagesFailedCounter.Add(1);
			activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			_logger.LogError(ex, "Kafkaへのエラーイベント送信に失敗しました: {ErrorType}", payload.ErrorType);
			throw;
		}
	}

	/// <summary>
	/// 未送信のメッセージをフラッシュする
	/// </summary>
	public void Flush(TimeSpan timeout)
	{
		_producer.Flush(timeout);
	}

	public void Dispose()
	{
		_producer.Flush(TimeSpan.FromSeconds(5));
		_producer.Dispose();
	}
}
