using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReplayGenerator.Domain;
using ReplayGenerator.Infrastructure;
using ReplayGenerator.Models;
using StackExchange.Redis;

namespace ReplayGenerator.Services;

public class ReplayGeneratorWorker : BackgroundService
{
	private readonly ILogger<ReplayGeneratorWorker> _logger;
	private readonly IConnectionMultiplexer _redis;
	private readonly ValkeyStateManager _stateManager;
	private readonly ShakeDetectionTracker _shakeTracker;
	private readonly EarthquakeTracker _earthquakeTracker;
	private readonly ReplayFileBuilder _replayBuilder;
	private readonly ObjectStorageClient _storageClient;
	private readonly ReplayRepository _repository;

	private const string PubSubChannel = "realtime:broadcast:v1";

	public ReplayGeneratorWorker(
		ILogger<ReplayGeneratorWorker> logger,
		IConnectionMultiplexer redis,
		ValkeyStateManager stateManager,
		ShakeDetectionTracker shakeTracker,
		EarthquakeTracker earthquakeTracker,
		ReplayFileBuilder replayBuilder,
		ObjectStorageClient storageClient,
		ReplayRepository repository)
	{
		_logger = logger;
		_redis = redis;
		_stateManager = stateManager;
		_shakeTracker = shakeTracker;
		_earthquakeTracker = earthquakeTracker;
		_replayBuilder = replayBuilder;
		_storageClient = storageClient;
		_repository = repository;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.LogInformation("ReplayGenerator ワーカー開始");

		await _shakeTracker.RestoreAsync();

		var subscriber = _redis.GetSubscriber();
		await subscriber.SubscribeAsync(RedisChannel.Literal(PubSubChannel), async (_, message) =>
		{
			if (stoppingToken.IsCancellationRequested) return;
			try
			{
				await ProcessMessage(message!);
			}
			catch (Exception ex)
			{
				_logger.LogWarning($"Pub/Sub メッセージ処理エラー: {ex.Message}");
			}
		});

		_logger.LogInformation("Pub/Sub 購読開始");

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				var snapshot = await _stateManager.GetRealtimeSnapshot();
				var (shouldGenerate, state) = await _shakeTracker.CheckTimerAsync(snapshot);
				if (shouldGenerate && state != null)
					await GenerateFromShake(state, snapshot);
			}
			catch (Exception ex)
			{
				_logger.LogWarning($"タイマーチェックエラー: {ex.Message}");
			}

			await Task.Delay(1000, stoppingToken);
		}

		await subscriber.UnsubscribeAsync(RedisChannel.Literal(PubSubChannel));
		_logger.LogInformation("ReplayGenerator ワーカー停止");
	}

	private async Task ProcessMessage(string message)
	{
		using var doc = JsonDocument.Parse(message);
		var root = doc.RootElement;

		if (!root.TryGetProperty("type", out var typeProp))
			return;

		var type = typeProp.GetString();

		switch (type)
		{
			case "shake_detected":
				if (root.TryGetProperty("eventId", out var shakeEventId))
					await _shakeTracker.OnShakeDetected(shakeEventId.GetString()!);
				break;

			case "EEW":
				if (root.TryGetProperty("event", out var eewEvent))
				{
					var snapshot = await _stateManager.GetRealtimeSnapshot();
					if (snapshot != null)
						await _shakeTracker.SetEewSnapshot(snapshot);
				}
				break;

			case "earthquake":
				if (root.TryGetProperty("operation", out var op) && op.GetString() == "upsert"
					&& root.TryGetProperty("event_id", out var eqEventId))
				{
					var recordJson = root.TryGetProperty("record", out var record) ? record.GetRawText() : "{}";
					var eqState = await _earthquakeTracker.OnEarthquakeUpsert(eqEventId.GetString()!, recordJson);
					if (eqState != null)
						await GenerateFromEarthquake(eqState);
				}
				break;
		}
	}

	private async Task GenerateFromShake(ShakeState state, string? snapshotJson)
	{
		_logger.LogInformation($"揺れ検知リプレイファイル生成開始: {state.ShakeEventId}");

		try
		{
			var startTime = state.StartTime.AddSeconds(-10);
			var endTime = state.LastEventTime.AddSeconds(5);

			var (fileBytes, fileName) = await _replayBuilder.BuildAsync(startTime, endTime, snapshotJson);

			using var stream = new MemoryStream(fileBytes);
			var objectKey = $"replay/{fileName}";
			var size = await _storageClient.UploadAsync(objectKey, stream);

			var replayFileId = await _repository.InsertReplayFile(startTime, endTime, objectKey, (int)size);
			await _repository.InsertTrigger(replayFileId, "shake_detection", state.ShakeEventId);

			_logger.LogInformation($"揺れ検知リプレイファイル生成完了: {fileName}");
		}
		catch (Exception ex)
		{
			_logger.LogWarning($"揺れ検知リプレイファイル生成エラー: {ex.Message}");
		}
		finally
		{
			await _shakeTracker.CompleteAsync();
		}
	}

	private async Task GenerateFromEarthquake(EarthquakeState state)
	{
		_logger.LogInformation($"地震情報リプレイファイル生成開始: {state.EventId}");

		try
		{
			var startTime = (state.OriginTime ?? state.ReportTime).AddSeconds(-30);
			var endTime = state.ReportTime.AddSeconds(30);

			var snapshotJson = await _stateManager.GetRealtimeSnapshot();
			var (fileBytes, fileName) = await _replayBuilder.BuildAsync(startTime, endTime, snapshotJson);

			using var stream = new MemoryStream(fileBytes);
			var objectKey = $"replay/{fileName}";
			var size = await _storageClient.UploadAsync(objectKey, stream);

			var replayFileId = await _repository.InsertReplayFile(startTime, endTime, objectKey, (int)size);
			await _repository.InsertTrigger(replayFileId, "earthquake", state.EventId);

			_logger.LogInformation($"地震情報リプレイファイル生成完了: {fileName}");
		}
		catch (Exception ex)
		{
			_logger.LogWarning($"地震情報リプレイファイル生成エラー: {ex.Message}");
		}
		finally
		{
			await _earthquakeTracker.CompleteAsync(state.EventId);
		}
	}
}
