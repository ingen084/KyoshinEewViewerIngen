using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ShakeDetectionProducer;

/// <summary>
/// Valkey Streamへのイベント送信を行うProducer
/// </summary>
public sealed class ValkeyStreamProducer : IAsyncDisposable
{
	private static readonly ActivitySource ActivitySource = new("ShakeDetectionProducer");
	private static readonly Meter Meter = new("ShakeDetectionProducer");

	private static readonly Counter<long> MessagesSentCounter = Meter.CreateCounter<long>(
		"valkey.messages_sent",
		"messages",
		"Total number of messages sent to Valkey Stream");

	private static readonly Counter<long> MessagesFailedCounter = Meter.CreateCounter<long>(
		"valkey.messages_failed",
		"messages",
		"Total number of failed message deliveries");

	private static readonly Histogram<double> ProduceDurationHistogram = Meter.CreateHistogram<double>(
		"valkey.produce_duration",
		"ms",
		"Time to produce message to Valkey Stream");

	private readonly IConnectionMultiplexer _connection;
	private readonly IDatabase _database;
	private readonly string _streamKey;
	private readonly int _maxLength;
	private readonly ILogger<ValkeyStreamProducer> _logger;

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
	};

	public ValkeyStreamProducer(ILogger<ValkeyStreamProducer> logger)
	{
		_logger = logger;

		var connectionString = Environment.GetEnvironmentVariable("VALKEY_CONNECTION_STRING") ?? "localhost:6379";
		_streamKey = Environment.GetEnvironmentVariable("VALKEY_STREAM_KEY") ?? "shake-detect-events";
		_maxLength = int.Parse(Environment.GetEnvironmentVariable("VALKEY_STREAM_MAXLEN") ?? "10000");

		var options = ConfigurationOptions.Parse(connectionString);
		options.ClientName = "shake-detection-producer";
		options.ConnectTimeout = 5000;
		options.SyncTimeout = 5000;
		options.AbortOnConnectFail = false;

		_connection = ConnectionMultiplexer.Connect(options);
		_connection.ConnectionFailed += (_, e) =>
			_logger.LogError("Valkey接続エラー: {FailureType} - {Exception}", e.FailureType, e.Exception?.Message);
		_connection.ConnectionRestored += (_, e) =>
			_logger.LogInformation("Valkey接続が復旧しました: {EndPoint}", e.EndPoint);

		_database = _connection.GetDatabase();

		_logger.LogInformation("Valkey Stream Producerを初期化しました: {ConnectionString}, StreamKey={StreamKey}, MaxLen={MaxLen}",
			connectionString, _streamKey, _maxLength);
	}

	/// <summary>
	/// 揺れ検知イベントをValkey Streamに送信する
	/// </summary>
	public async Task ProduceShakeDetectedAsync(ShakeDetectedPayload payload, CancellationToken cancellationToken = default)
	{
		using var activity = ActivitySource.StartActivity("valkey.produce.shake_detected");
		activity?.SetTag("event.id", payload.EventId.ToString());
		activity?.SetTag("event.level", payload.Level);

		var stopwatch = Stopwatch.StartNew();

		try
		{
			var json = JsonSerializer.Serialize<StreamPayload>(payload, JsonOptions);
			var entries = new NameValueEntry[]
			{
				new("eventId", payload.EventId.ToString()),
				new("type", "shake_detected"),
				new("payload", json)
			};

			var messageId = await _database.StreamAddAsync(
				_streamKey,
				entries,
				maxLength: _maxLength,
				useApproximateMaxLength: true);

			stopwatch.Stop();
			ProduceDurationHistogram.Record(stopwatch.Elapsed.TotalMilliseconds);
			MessagesSentCounter.Add(1);

			activity?.SetTag("valkey.message_id", messageId.ToString());

			_logger.LogDebug("Valkey Streamにメッセージを送信しました: MessageId={MessageId}", messageId);
		}
		catch (RedisException ex)
		{
			stopwatch.Stop();
			MessagesFailedCounter.Add(1);
			activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			_logger.LogError(ex, "Valkey Streamへのメッセージ送信に失敗しました: {EventId}", payload.EventId);
			throw;
		}
	}

	/// <summary>
	/// エラーイベントをValkey Streamに送信する
	/// </summary>
	public async Task ProduceErrorAsync(ErrorPayload payload, CancellationToken cancellationToken = default)
	{
		using var activity = ActivitySource.StartActivity("valkey.produce.error");
		activity?.SetTag("error.type", payload.ErrorType);

		var stopwatch = Stopwatch.StartNew();

		try
		{
			var json = JsonSerializer.Serialize<StreamPayload>(payload, JsonOptions);
			var entries = new NameValueEntry[]
			{
				new("eventId", $"error-{payload.Time:yyyyMMddHHmmss}"),
				new("type", "error"),
				new("payload", json)
			};

			var messageId = await _database.StreamAddAsync(
				_streamKey,
				entries,
				maxLength: _maxLength,
				useApproximateMaxLength: true);

			stopwatch.Stop();
			ProduceDurationHistogram.Record(stopwatch.Elapsed.TotalMilliseconds);
			MessagesSentCounter.Add(1);

			_logger.LogDebug("エラーイベントをValkey Streamに送信しました: MessageId={MessageId}", messageId);
		}
		catch (RedisException ex)
		{
			stopwatch.Stop();
			MessagesFailedCounter.Add(1);
			activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			_logger.LogError(ex, "Valkey Streamへのエラーイベント送信に失敗しました: {ErrorType}", payload.ErrorType);
			throw;
		}
	}

	/// <summary>
	/// コンシューマーグループを作成する（存在しない場合）
	/// </summary>
	public async Task EnsureConsumerGroupAsync(string groupName)
	{
		try
		{
			// Stream が存在しない場合は MKSTREAM オプションで作成
			await _database.StreamCreateConsumerGroupAsync(_streamKey, groupName, StreamPosition.NewMessages, createStream: true);
			_logger.LogInformation("コンシューマーグループを作成しました: {GroupName}", groupName);
		}
		catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
		{
			// グループが既に存在する場合は無視
			_logger.LogDebug("コンシューマーグループは既に存在します: {GroupName}", groupName);
		}
	}

	public async ValueTask DisposeAsync()
	{
		await _connection.CloseAsync();
		_connection.Dispose();
	}
}
