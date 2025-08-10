using Avalonia.Platform;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;
using KyoshinMonitorLib;
using KyoshinMonitorLib.SkiaImages;
using KyoshinMonitorLib.UrlGenerator;
using MessagePack;
using Sentry;
using SkiaSharp;
using Splat;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Services;

public class KyoshinMonitorWatchService
{
	private HttpClient HttpClient { get; } = new(new HttpClientHandler()
	{
		AutomaticDecompression = DecompressionMethods.All
	})
	{ Timeout = TimeSpan.FromSeconds(2) };

	private ILogger Logger { get; }
	private KyoshinEewViewerConfiguration Config { get; }

	private KyoshinMonitorLib.ApiResult.WebApi.Eew? LatestEew { get; set; }
	private EewController EewController { get; }
	private RealtimeObservationPoint[]? Points { get; set; }

	private Stopwatch Stopwatch { get; } = Stopwatch.StartNew();
	/// <summary>
	/// タイムシフトなども含めた現在時刻
	/// </summary>
	public DateTime CurrentDisplayTime => LastElapsedDelayedTime + Stopwatch.Elapsed;
	private DateTime LastElapsedDelayedTime { get; set; }

	public event Action<(DateTime time, RealtimeObservationPoint[] data, KyoshinEvent[] events)>? RealtimeDataUpdated;
	public event Action<DateTime>? RealtimeDataParseProcessStarted;
	public event Action<string>? WarningMessageUpdated;

	public KyoshinMonitorWatchService(ILogManager logManager, KyoshinEewViewerConfiguration config, EewController eewControlService)
	{
		Logger = logManager.GetLogger<KyoshinMonitorWatchService>();
		EewController = eewControlService;
		Config = config;
	}

	public void Initalize()
	{
		if (!TravelTimeTableService.IsInitialized)
		{
			Logger.LogInfo("走時表を準備しています。");
			TravelTimeTableService.Initialize();
			Logger.LogInfo($"走時表を準備しました。");
		}

		if (Points == null)
		{
			Logger.LogInfo("観測点情報を読み込んでいます。");
			using (var stream = AssetLoader.Open(new Uri("avares://KyoshinEewViewer/Assets/ShindoObsPoints.mpk.lz4", UriKind.Absolute)) ?? throw new Exception("観測点情報が読み込めません"))
			{
				var points = MessagePackSerializer.Deserialize<ObservationPoint[]>(stream, options: MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block));
				Points = points.Where(p => p.Point != null && !p.IsSuspended).Select(p => new RealtimeObservationPoint(p)).ToArray();
			}
			Logger.LogInfo($"観測点情報を読み込みました。");

			foreach (var point in Points)
				// 60キロ以内の近い順の最大15観測点を関連付ける
				// 生活振動が多い神奈川･東京は20観測点とする
				point.NearPoints = Points
					.Where(p => point != p && point.Location.Distance(p.Location) < 60)
					.OrderBy(p => point.Location.Distance(p.Location))
					.Take(point.Region is "神奈川県" or "東京都" ? 20 : 15)
					.ToArray();
		}
		WarningMessageUpdated?.Invoke("初回のデータ取得中です。しばらくお待ち下さい。");
	}

	private bool IsRunning { get; set; }
	public async Task TimerElapsed(DateTime time)
	{
		// 観測点が読み込みできていなければ処理しない
		if (Points == null)
			return;

		// 時刻が変化したときのみ
		if (LastElapsedDelayedTime != time)
			Stopwatch.Restart();
		LastElapsedDelayedTime = time;

		// 通信量制限モードが有効であればその間隔以外のものについては処理しない
		if (Config.KyoshinMonitor.FetchFrequency > 1
		 && (!EewController.Found || !Config.KyoshinMonitor.ForcefetchOnEew)
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
			using (var stream = await response.Content.ReadAsStreamAsync())
			{
				var bitmap = SKBitmap.Decode(stream);
				if (bitmap != null)
					using (bitmap)
						ProcessImage(bitmap, time);
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
			RealtimeDataUpdated?.Invoke((time, Points, KyoshinEvents.ToArray()));

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
		if (Points == null)
			return;

		if (bitmapBytes != null)
		{
			var bitmap = SKBitmap.Decode(bitmapBytes);
			using (bitmap)
				ProcessImage(bitmap, time);
		}

		if (!string.IsNullOrEmpty(eewJson) && Config.Eew.EnableKyoshinMonitor)
		{
			var eewResult = new ApiResult<KyoshinMonitorLib.ApiResult.WebApi.Eew>(HttpStatusCode.OK, JsonSerializer.Deserialize<KyoshinMonitorLib.ApiResult.WebApi.Eew>(json: eewJson));

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
		RealtimeDataUpdated?.Invoke((time, Points, KyoshinEvents.ToArray()));
	}

	private List<KyoshinEvent> KyoshinEvents { get; } = [];
	private void ProcessImage(SKBitmap bitmap, DateTime time)
	{
		if (Points == null)
			return;

		// パース
		foreach (var point in Points)
		{
			var color = bitmap.GetPixel(point.ImageLocation.X, point.ImageLocation.Y);
			if (color.Alpha != 255)
			{
				point.Update(null, null);
				continue;
			}
			var intensity = ColorConverter.ConvertToIntensityFromScale(ColorConverter.ConvertToScaleAtPolynomialInterpolation(color));
			point.Update(color, (float)intensity);
		}

		// イベントチェック･異常値除外
		foreach (var point in Points)
		{
			// 異常値の排除
			if (point.LatestIntensity is { } latestIntensity &&
				point.IntensityDiff < 1 && point.Event == null &&
				latestIntensity >= (point.HasNearPoints ? 3 : 5) && // 震度3以上 離島は5以上
				Math.Abs(point.IntensityAverage - latestIntensity) <= 1 && // 10秒間平均で 1.0 の範囲
				(
					point.IsTmpDisabled || (point.NearPoints?.All(p => (latestIntensity - (p.LatestIntensity ?? -3)) >= 3) ?? true)
				))
			{
				if (!point.IsTmpDisabled)
					Logger.LogInfo($"異常値の判定により観測点の除外を行いました: {point.Code} {point.LatestIntensity} {point.IntensityAverage}");
				point.IsTmpDisabled = true;
			}
			else if (point.LatestIntensity != null && point.IsTmpDisabled)
			{
				Logger.LogInfo($"異常値による除外を戻します: {point.Code} {point.LatestIntensity} {point.IntensityAverage}");
				point.IsTmpDisabled = false;
			}

			// 除外されている観測点はイベントの検出に使用しない
			if (point.IsTmpDisabled)
				continue;

			if (point.IntensityDiff < 1.1)
			{
				// 未来もしくは過去のイベントは離脱
				if (point.Event is { } evt && (point.EventedAt > time || point.EventedExpireAt < time))
				{
					Logger.LogDebug($"揺れ検知終了: {point.Code} {evt.Id} {time} {point.EventedAt} {point.EventedExpireAt}");
					point.Event = null;
					evt.RemovePoint(point);

					if (evt.PointCount <= 0)
					{
						KyoshinEvents.Remove(evt);
						Logger.LogDebug($"イベント終了: {evt.Id}");
					}
				}
				continue;
			}
			// 周囲の観測点が未計算の場合もしくは欠測の場合戻る
			if (point.NearPoints == null || point.LatestIntensity == null)
				continue;

			// 有効な周囲の観測点の数
			var availableNearCount = point.NearPoints.Count(n => n.HasValidHistory);

			// 周囲の観測点が存在しない場合 2 以上でeventedとしてマーク
			if (availableNearCount == 0)
			{
				if (point.IntensityDiff >= 2 && point.Event == null)
				{
					point.Event = new(time, point);
					point.EventedAt = time;
					KyoshinEvents.Add(point.Event);
					Logger.LogDebug($"揺れ検知(単独): {point.Code} 変位: {point.IntensityDiff} {point.Event.Id}");
				}
				continue;
			}

			var events = new List<KyoshinEvent>();
			if (point.Event != null)
				events.Add(point.Event);
			var count = 0;
			// 周囲の観測点の 1/3 以上 0.5 であればEventedとしてマーク
			var threshold = Math.Min(availableNearCount, Math.Max(availableNearCount / 3, 4));
			// 東京･神奈川の場合はちょっと閾値を高くする
			if (point.Region is "東京都" or "神奈川県")
				threshold = Math.Min(availableNearCount, (int)Math.Max(availableNearCount / 1.5, 4));

			foreach (var np in point.NearPoints)
			{
				if (!np.IsTmpDisabled && np.IntensityDiff >= 0.5)
				{
					count++;
					if (np.Event != null)
						events.Add(np.Event);
				}
			}
			if (count < threshold)
				continue;

			// この時点で検知扱い
			point.EventedAt = time;

			var uniqueEvents = events.Distinct();
			// 複数件ある場合イベントをマージする
			if (uniqueEvents.Count() > 1)
			{
				// createdAt が一番古いイベントにマージする
				var firstEvent = uniqueEvents.OrderBy(e => e.CreatedAt).First();
				foreach (var evt in uniqueEvents)
				{
					if (evt == firstEvent)
						continue;
					firstEvent.MergeEvent(evt);
					KyoshinEvents.Remove(evt);
					Logger.LogDebug($"イベント統合: {firstEvent.Id} <- {evt.Id}");
				}

				// マージしたイベントと異なる状態だった場合追加
				if (point.Event == firstEvent)
					continue;
				if (point.Event == null)
					Logger.LogDebug($"揺れ検知: {point.Code} {firstEvent.Id} 利用数:{count} 閾値:{threshold} 総数:{point.NearPoints.Length}");
				firstEvent.AddPoint(point, time);
				continue;
			}
			// 1件の場合はイベントに追加
			if (uniqueEvents.Any())
			{
				if (point.Event == null)
					Logger.LogDebug($"揺れ検知: {point.Code} {events[0].Id} 利用数:{count} 閾値:{threshold} 総数:{point.NearPoints.Length}");
				events[0].AddPoint(point, time);
				continue;
			}

			// 存在しなかった場合はイベント作成
			if (point.Event == null)
			{
				point.Event = new(time, point);
				KyoshinEvents.Add(point.Event);
				Logger.LogDebug($"揺れ検知(新規): {point.Code} {point.Event.Id} 利用数:{count} 閾値:{threshold} 総数:{point.NearPoints.Length}");
			}
		}

		// イベントの紐づけ
		foreach (var evt in KyoshinEvents.OrderBy(e => e.CreatedAt).ToArray())
		{
			if (!KyoshinEvents.Contains(evt))
				continue;

			// 2つのイベントが 一定距離未満の場合マージする
			foreach (var evt2 in KyoshinEvents.Where(e => e != evt && evt.CheckNearby(e)).ToArray())
			{
				evt.MergeEvent(evt2);
				KyoshinEvents.Remove(evt2);
				Logger.LogDebug($"イベント距離統合: {evt.Id} <- {evt2.Id}");
			}
		}
	}

	public void ResetHistories()
	{
		if (Points == null)
			return;
		foreach (var point in Points)
		{
			point.ResetHistory();
			point.Event = null;
			point.EventedExpireAt = DateTime.MinValue;
		}
		KyoshinEvents.Clear();
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
