using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.KyoshinMonitorObservationPoint;
using KyoshinEewViewer.Core.ShakeDetection;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;
using KyoshinMonitorLib;
using KyoshinMonitorLib.UrlGenerator;
using Sentry;
using SkiaSharp;
using Splat;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Services;

public class KyoshinMonitorWatchService
{
	private static HttpClient HttpClient { get; } = new(new HttpClientHandler()
	{
		AutomaticDecompression = DecompressionMethods.All,
		MaxConnectionsPerServer = 1,
	})
	{ Timeout = TimeSpan.FromSeconds(2) };

	private ILogger Logger { get; }
	private KyoshinEewViewerConfiguration Config { get; }
	private ObservationPointsUpdateService ObservationPointsUpdateService { get; }

	private KyoshinMonitorLib.ApiResult.WebApi.Eew? LatestEew { get; set; }
	private EewController EewController { get; }
	private ShakeDetectionEngine ShakeDetectionEngine { get; }

	private Stopwatch Stopwatch { get; } = Stopwatch.StartNew();
	/// <summary>
	/// タイムシフトなども含めた現在時刻
	/// </summary>
	public DateTime CurrentDisplayTime => LastElapsedDelayedTime + Stopwatch.Elapsed;
	private DateTime LastElapsedDelayedTime { get; set; }
	
	public ObservationPointsFileHeader? PointsFileHeader { get; private set; }

	public event Action<(DateTime time, RealtimeObservationPoint[] data, KyoshinEvent[] events)>? RealtimeDataUpdated;
	public event Action<DateTime>? RealtimeDataParseProcessStarted;
	public event Action<string>? WarningMessageUpdated;

	public KyoshinMonitorWatchService(ILogManager logManager, KyoshinEewViewerConfiguration config, EewController eewControlService, ObservationPointsUpdateService observationPointsUpdateService)
	{
		Logger = logManager.GetLogger<KyoshinMonitorWatchService>();
		EewController = eewControlService;
		Config = config;
		ObservationPointsUpdateService = observationPointsUpdateService;
		ShakeDetectionEngine = new ShakeDetectionEngine(logManager);
	}

	public async Task Initalize()
	{
		if (!TravelTimeTableService.IsInitialized)
		{
			Logger.LogInfo("走時表を準備しています。");
			TravelTimeTableService.Initialize();
			Logger.LogInfo($"走時表を準備しました。");
		}

		if (ShakeDetectionEngine.Points == null)
		{
			Logger.LogInfo("観測点情報を読み込んでいます。");

			// ObservationPointsUpdateServiceから観測点データを取得
			var observationPoints = await ObservationPointsUpdateService.GetObservationPointsAsync();
			PointsFileHeader = ObservationPointsUpdateService.CurrentHeader;

			// RealtimeObservationPointを新規作成
			var points = observationPoints
				.Where(p => p is { Point: not null, IsSuspended: false })
				.Select(p => new RealtimeObservationPoint(p))
				.ToArray();

			// ShakeDetectionEngineを初期化
			ShakeDetectionEngine.Initialize(points);

			Logger.LogInfo($"観測点情報を読み込みました。");
		}
		WarningMessageUpdated?.Invoke("初回のデータ取得中です。しばらくお待ち下さい。");
	}

	private bool IsRunning { get; set; }
	public async Task TimerElapsed(DateTime time)
	{
		// 観測点が読み込みできていなければ処理しない
		if (ShakeDetectionEngine.Points == null)
			return;

		// 時刻が変化したときのみ
		if (LastElapsedDelayedTime != time)
			Stopwatch.Restart();
		LastElapsedDelayedTime = time;

		// 通信量制限モードが有効であればその間隔以外のものについては処理しない
		var hasConfirmedShakeDetect = ShakeDetectionEngine.KyoshinEvents.Any(e => e.IsConfirmed && e.Level >= Config.KyoshinMonitor.EventNotificationLevel);
		if (Config.KyoshinMonitor.FetchFrequency > 1
		 && (!EewController.Found || !Config.KyoshinMonitor.ForcefetchOnEew)
		 && (!hasConfirmedShakeDetect || !Config.KyoshinMonitor.ForcefetchOnShakeDetect)
		 && ((DateTimeOffset)time).ToUnixTimeSeconds() % Config.KyoshinMonitor.FetchFrequency != 0)
			return;

		// すでに処理中であれば戻る
		if (IsRunning)
			return;

		// 無効化時はなにもしない
		if (Config.KyoshinMonitor.ReceiveMode == KyoshinEewViewerConfiguration.KyoshinMonitorConfig.Mode.None)
		{
			RealtimeDataUpdated?.Invoke((time, [], []));
			return;
		}

		IsRunning = true;
		RealtimeDataParseProcessStarted?.Invoke(time);
		var trans = SentrySdk.StartTransaction("kyoshin-monitor", "process");
		var imageUrl = Config.KyoshinMonitor.ReceiveMode switch
		{
			KyoshinEewViewerConfiguration.KyoshinMonitorConfig.Mode.Kmoni => WebApiUrlGenerator.Generate(WebApiUrlType.RealtimeImg, time, RealtimeDataType.Shindo, false),
			KyoshinEewViewerConfiguration.KyoshinMonitorConfig.Mode.Lmoni => LpgmWebApiUrlGenerator.Generate(LpgmWebApiUrlType.RealtimeImg, time, RealtimeDataType.Shindo, false),
			_ => throw new InvalidOperationException("不明な受信モードです")
		};
		try
		{
			// 画像をGET
			using var response = await HttpClient.GetAsync(imageUrl);
			if (response.StatusCode != HttpStatusCode.OK)
			{
				if (Config.Timer.AutoOffsetIncrement)
				{
					WarningMessageUpdated?.Invoke($"{time:HH:mm:ss} オフセットを調整しました。");
					Config.Timer.Offset = Math.Min(5000, Config.Timer.Offset + 100);
					return;
				}

				WarningMessageUpdated?.Invoke($"{time:HH:mm:ss} オフセットを調整してください。");
				return;
			}
			// オフセットが大きい場合1分に1回短縮を試みる
			if (time.Second == 0 && Config.Timer.AutoOffsetIncrement && Config.Timer.Offset > 1100)
				Config.Timer.Offset -= 100;

			//画像から取得
			byte[]? imageBytes = null;
			using (var stream = await response.Content.ReadAsStreamAsync())
			{
				// 設定が有効な場合は画像データを保持
				if (Config.KyoshinMonitor.SaveResponseOnHighMissingRate)
				{
					using var memoryStream = new MemoryStream();
					await stream.CopyToAsync(memoryStream);
					imageBytes = memoryStream.ToArray();

					// Content-Lengthと実際のレスポンスサイズを比較
					var contentLength = response.Content.Headers.ContentLength;
					if (contentLength.HasValue && imageBytes.Length != contentLength.Value)
					{
						Logger.LogWarning($"{time:yyyy/MM/dd HH:mm:ss} レスポンスサイズが不一致です: Content-Length={contentLength.Value}, 実際のサイズ={imageBytes.Length}");
					}

					using var imageStream = new MemoryStream(imageBytes);
					var bitmap = SKBitmap.Decode(imageStream);
					if (bitmap != null)
						using (bitmap)
							ShakeDetectionEngine.ProcessImage(bitmap, time);
				}
				else
				{
					var bitmap = SKBitmap.Decode(stream);
					if (bitmap != null)
						using (bitmap)
							ShakeDetectionEngine.ProcessImage(bitmap, time);
				}
			}

			// 観測点の欠損率をチェック
			if (ShakeDetectionEngine.Points is { } points)
			{
				var totalPoints = points.Length;
				var missingPoints = points.Count(p => p.LatestIntensity == null);
				var missingRate = (double)missingPoints / totalPoints;
				if (missingRate >= 0.5)
				{
					Logger.LogWarning($"{time:yyyy/MM/dd HH:mm:ss} 観測点の欠損率が高くなっています: {missingPoints}/{totalPoints} ({missingRate:P1})");

					// 設定が有効な場合はレスポンス画像を保存
					if (Config.KyoshinMonitor.SaveResponseOnHighMissingRate && imageBytes != null)
					{
						try
						{
							var saveDirectory = Path.Combine(PlatformDirectories.ApplicationData, "HighMissingRateResponses");
							Directory.CreateDirectory(saveDirectory);
							var fileName = $"{time:yyyyMMdd_HHmmss}_{(int)Math.Floor(missingRate * 100):D2}.png";
							var filePath = Path.Combine(saveDirectory, fileName);
							await File.WriteAllBytesAsync(filePath, imageBytes);
							Logger.LogInfo($"レスポンス画像を保存しました: {filePath}");
						}
						catch (Exception ex)
						{
							Logger.LogWarning(ex, "レスポンス画像の保存に失敗しました");
						}
					}
				}
			}

			if (Config.Eew.EnableKyoshinMonitor)
			{
				var eewResult = await GetEewInfo(time);

				// 新しい情報の場合のみ更新を通知する
				if (eewResult.Data?.ReportId != LatestEew?.ReportId ||
					eewResult.Data?.ReportNum > LatestEew?.ReportNum)
				{
					// キャンセルの場合はキャンセル通知
					if (string.IsNullOrEmpty(eewResult.Data?.ReportId))
						EewController.Cancelled(null, time);
					else
						EewController.Update(
							new Models.Eew
							{
								Source = EewSource.KyoshinMonitor,
								DisplaySource = "強震モニタ",
								Id = eewResult.Data.ReportId,
								SerialNo = eewResult.Data.ReportNum ?? 0,
								IsFinal = eewResult.Data.IsFinal ?? false,
								MaxIntensity = eewResult.Data.Calcintensity ?? JmaIntensity.Error,
								Hypocenter = new EewHypocenter()
								{
									OccurrenceTime = eewResult.Data.OriginTime ?? time,
									Place = eewResult.Data.RegionName,
									Location = eewResult.Data.Location,
									Magnitude = eewResult.Data.Magunitude ?? 0,
									Depth = eewResult.Data.Depth ?? 0,
									IsTemporary = eewResult.Data.Depth == 10 && eewResult.Data.Magunitude is { } m && Math.Abs(m - 1.0) < 0.01,
								},
								ReceiveTime = eewResult.Data.ReportTime ?? time,
								IsWarning = eewResult.Data.IsAlert,
							}, time);
				}
				LatestEew = eewResult.Data;
			}
			// 確定済みイベントのみを送信
			var confirmedEvents = ShakeDetectionEngine.KyoshinEvents.Where(e => e.IsConfirmed).ToArray();
			RealtimeDataUpdated?.Invoke((time, ShakeDetectionEngine.Points!, confirmedEvents));

			trans.Finish(SpanStatus.Ok);
		}
		catch (AggregateException ex)
		{
			WarningMessageUpdated?.Invoke($"{time:HH:mm:ss} 取得エラー");
			Logger.LogWarning(ex, "取得に失敗しました。");
			trans.Finish(ex, SpanStatus.InternalError);
		}
		catch (TaskCanceledException ex)
		{
			WarningMessageUpdated?.Invoke($"{time:HH:mm:ss} タイムアウトしました。");
			Logger.LogWarning(ex, "取得にタイムアウトしました。");
			trans.Finish(ex, SpanStatus.DeadlineExceeded);
		}
		catch (KyoshinMonitorException ex)
		{
			WarningMessageUpdated?.Invoke($"{time:HH:mm:ss} {ex.Message}");
			Logger.LogWarning(ex, "取得にタイムアウトしました。");
			trans.Finish(ex, SpanStatus.DeadlineExceeded);
		}
		catch (HttpRequestException ex)
		{
			WarningMessageUpdated?.Invoke($"{time:HH:mm:ss} HTTPエラー");
			Logger.LogWarning(ex, "HTTPエラー");
			trans.Finish(ex);
		}
		catch (Exception ex)
		{
			WarningMessageUpdated?.Invoke($"{time:HH:mm:ss} 汎用エラー({ex.Message})");
			Logger.LogWarning(ex, "汎用エラー");
			trans.Finish(ex);
		}
		finally
		{
			IsRunning = false;
		}
	}

	public void LoadImageForReplay(DateTime time, byte[]? bitmapBytes, string? eewJson)
	{
		// 観測点が読み込みできていなければ処理しない
		if (ShakeDetectionEngine.Points == null)
			return;

		if (bitmapBytes != null)
		{
			var bitmap = SKBitmap.Decode(bitmapBytes);
			using (bitmap)
				ShakeDetectionEngine.ProcessImage(bitmap, time);
		}

		if (!string.IsNullOrEmpty(eewJson) && Config.Eew.EnableKyoshinMonitor)
		{
			var eewResult = new ApiResult<KyoshinMonitorLib.ApiResult.WebApi.Eew?>(
				HttpStatusCode.OK,
				JsonSerializer.Deserialize<KyoshinMonitorLib.ApiResult.WebApi.Eew>(json: eewJson)
			);

			// 新しい情報の場合のみ更新を通知する
			if (eewResult.Data?.ReportId != LatestEew?.ReportId ||
				eewResult.Data?.ReportNum > LatestEew?.ReportNum)
			{
				// キャンセルの場合はキャンセル通知
				if (string.IsNullOrEmpty(eewResult.Data?.ReportId))
					EewController.Cancelled(null, time);
				else
					EewController.Update(
						new Models.Eew
						{
							Source = EewSource.KyoshinMonitor,
							DisplaySource = "強震モニタ",
							Id = eewResult.Data.ReportId,
							SerialNo = eewResult.Data.ReportNum ?? 0,
							IsFinal = eewResult.Data.IsFinal ?? false,
							MaxIntensity = eewResult.Data.Calcintensity ?? JmaIntensity.Error,
							Hypocenter = new EewHypocenter()
							{
								OccurrenceTime = eewResult.Data.OriginTime ?? time,
								Place = eewResult.Data.RegionName,
								Location = eewResult.Data.Location,
								Magnitude = eewResult.Data.Magunitude ?? 0,
								Depth = eewResult.Data.Depth ?? 0,
								IsTemporary = eewResult.Data.Depth == 10 && eewResult.Data.Magunitude is { } m && Math.Abs(m - 1.0) < 0.01,
							},
							ReceiveTime = eewResult.Data.ReportTime ?? time,
							IsWarning = eewResult.Data.IsAlert,
						}, time);
			}
			LatestEew = eewResult.Data;
		}
		// 確定済みイベントのみを送信
		var confirmedEvents = ShakeDetectionEngine.KyoshinEvents.Where(e => e.IsConfirmed).ToArray();
		RealtimeDataUpdated?.Invoke((time, ShakeDetectionEngine.Points, confirmedEvents));
	}

	public void ResetHistories()
	{
		ShakeDetectionEngine.ResetHistories();
		LatestEew = null;
	}

	private async Task<ApiResult<KyoshinMonitorLib.ApiResult.WebApi.Eew?>> GetEewInfo(DateTime time)
	{
		var url = Config.KyoshinMonitor.ReceiveMode switch
		{
			KyoshinEewViewerConfiguration.KyoshinMonitorConfig.Mode.Kmoni => WebApiUrlGenerator.Generate(WebApiUrlType.EewJson, time),
			KyoshinEewViewerConfiguration.KyoshinMonitorConfig.Mode.Lmoni => LpgmWebApiUrlGenerator.Generate(LpgmWebApiUrlType.EewJson, time),
			_ => throw new InvalidOperationException("不明な受信モードです")
		};
		try
		{
			using var response = await HttpClient.GetAsync(url);
			if (!response.IsSuccessStatusCode)
				return new ApiResult<KyoshinMonitorLib.ApiResult.WebApi.Eew?>(response.StatusCode, null);
			var statusCode = response.StatusCode;
			return new ApiResult<KyoshinMonitorLib.ApiResult.WebApi.Eew?>(statusCode, JsonSerializer.Deserialize<KyoshinMonitorLib.ApiResult.WebApi.Eew>(await response.Content.ReadAsStringAsync()));
		}
		catch (TaskCanceledException)
		{
			throw new KyoshinMonitorException("Request Timeout: " + url);
		}
	}
}
