using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;
using KyoshinEewViewer.Services;
using KyoshinMonitorLib;
using KyoshinMonitorLib.SkiaImages;
using KyoshinMonitorLib.UrlGenerator;
using Sentry;
using SkiaSharp;
using Splat;
using System;
using System.Collections.Generic;
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
	private HttpClient HttpClient { get; } = new(new HttpClientHandler()
	{
		AutomaticDecompression = DecompressionMethods.All
	})
	{ Timeout = TimeSpan.FromSeconds(2) };
	private ILogger Logger { get; }
	private TimerService TimerService { get; }
	private KyoshinEewViewerConfiguration Config { get; }

	private KyoshinMonitorLib.ApiResult.WebApi.Eew? LatestEew { get; set; }
	private EewController EewController { get; }
	private WebApi WebApi { get; }
	private RealtimeObservationPoint[]? Points { get; set; }

	/// <summary>
	/// タイムシフトなども含めた現在時刻
	/// </summary>
	public DateTime CurrentDisplayTime => LastElapsedDelayedTime + (DateTime.Now - LastElapsedDelayedLocalTime);
	private DateTime LastElapsedDelayedTime { get; set; }
	private DateTime LastElapsedDelayedLocalTime { get; set; }

	public DateTime? OverrideDateTime { get; set; }
	public string? OverrideSource { get; set; }

	private int PreviousTimeshiftSeconds { get; set; }


	public event Action<(DateTime time, RealtimeObservationPoint[] data, KyoshinEvent[] events)>? RealtimeDataUpdated;
	public event Action<DateTime>? RealtimeDataParseProcessStarted;

	public KyoshinMonitorWatchService(ILogManager logManager, KyoshinEewViewerConfiguration config, EewController eewControlService, TimerService timer)
	{
		Logger = logManager.GetLogger<KyoshinMonitorWatchService>();
		EewController = eewControlService;
		TimerService = timer;
		Config = config;
		TimerService.DelayedTimerElapsed += t => TimerElapsed(t).Wait();
		WebApi = new WebApi() { Timeout = TimeSpan.FromSeconds(2) };
	}

	public void Start()
	{
		var sw = Stopwatch.StartNew();
		Logger.LogInfo("走時表を準備しています。");
		TravelTimeTableService.Initialize();
		Logger.LogInfo($"走時表を準備しました。 {sw.ElapsedMilliseconds}ms");

		sw.Restart();
		Logger.LogInfo("観測点情報を読み込んでいます。");
		using (var stream = new MemoryStream(Properties.Resources.ShindoObsPoints))
		{
			var points = ObservationPoint.LoadFromMpk(stream, true);
			Points = points.Where(p => p.Point != null && !p.IsSuspended).Select(p => new RealtimeObservationPoint(p)).ToArray();
		}
		Logger.LogInfo($"観測点情報を読み込みました。 {sw.ElapsedMilliseconds}ms");

		foreach (var point in Points)
			// 60キロ以内の近い順の最大15観測点を関連付ける
			// 生活振動が多い神奈川･東京は17観測点とする
			point.NearPoints = Points
				.Where(p => point != p && point.Location.Distance(p.Location) < 60)
				.OrderBy(p => point.Location.Distance(p.Location))
				.Take(point.Region is "神奈川県" or "東京都" ? 17 : 15)
				.ToArray();

		TimerService.StartMainTimer();
		DisplayWarningMessageUpdated.SendWarningMessage("初回のデータ取得中です。しばらくお待ち下さい。");
	}

	private bool IsRunning { get; set; }
	private async Task TimerElapsed(DateTime realTime)
	{
		// 観測点が読み込みできていなければ処理しない
		if (Points == null)
			return;

		var time = realTime;
		// リプレイ中であれば時刻を強制的に補正します
		if (OverrideDateTime is { } overrideDateTime)
		{
			time = overrideDateTime;
			OverrideDateTime = overrideDateTime.AddSeconds(1);
		}
		// タイムシフト中なら加算します(やっつけ)
		else if (Config.Timer.TimeshiftSeconds < 0)
			time = time.AddSeconds(Config.Timer.TimeshiftSeconds);

		// タイムシフトのスライダーが操作されていたら震度の履歴を削除し誤検知を防ぐ
		if (PreviousTimeshiftSeconds != Config.Timer.TimeshiftSeconds)
			foreach (var p in Points) p.ResetHistory();
		PreviousTimeshiftSeconds = Config.Timer.TimeshiftSeconds;

		LastElapsedDelayedTime = time;
		LastElapsedDelayedLocalTime = DateTime.Now;

		// 通信量制限モードが有効であればその間隔以外のものについては処理しない
		if (Config.KyoshinMonitor.FetchFrequency > 1
		 && (!EewController.Found || !Config.KyoshinMonitor.ForcefetchOnEew)
		 && ((DateTimeOffset)time).ToUnixTimeSeconds() % Config.KyoshinMonitor.FetchFrequency != 0)
			return;

		// すでに処理中であれば戻る
		if (IsRunning)
			return;
		IsRunning = true;
		RealtimeDataParseProcessStarted?.Invoke(time);
		var trans = SentrySdk.StartTransaction("kyoshin-monitor", "process");
		try
		{
			await Task.WhenAll([
				Task.Run(async () =>
				{
					try
					{
						if (OverrideSource != null)
						{
							var path = WebApiUrlGenerator.Generate(WebApiUrlType.RealtimeImg, time, RealtimeDataType.Shindo, false).Replace("http://www.kmoni.bosai.go.jp/", "");
							var file = Path.Combine(OverrideSource, path);
							if (!File.Exists(file))
							{
								Logger.LogInfo($"{time:HH:mm:ss} 画像ファイル {file} が見つかりません。リアルタイムに戻ります。");
								DisplayWarningMessageUpdated.SendWarningMessage($"{time:HH:mm:ss} 画像ファイルが見つかりません。リアルタイムに戻ります。");
								OverrideDateTime = null;
								OverrideSource = null;
								return;
							}
							await using var stream = File.OpenRead(file);
							//画像から取得
							using var bitmap = SKBitmap.Decode(stream);
							ProcessImage(bitmap, time);
						}
						else
						{
							// 画像をGET
							using var response = await HttpClient.GetAsync(WebApiUrlGenerator.Generate(WebApiUrlType.RealtimeImg, time, RealtimeDataType.Shindo, false));
							if (response.StatusCode != HttpStatusCode.OK)
							{
								if (Config.Timer.TimeshiftSeconds < 0)
								{
									DisplayWarningMessageUpdated.SendWarningMessage($"{time:HH:mm:ss} 利用できませんでした。({response.StatusCode})");
									return;
								}
								if (Config.Timer.AutoOffsetIncrement)
								{
									DisplayWarningMessageUpdated.SendWarningMessage($"{time:HH:mm:ss} オフセットを調整しました。");
									Config.Timer.Offset = Math.Min(5000, Config.Timer.Offset + 100);
									return;
								}

								DisplayWarningMessageUpdated.SendWarningMessage($"{time:HH:mm:ss} オフセットを調整してください。");
								return;
							}
							// オフセットが大きい場合1分に1回短縮を試みる
							if (time.Second == 0 && Config.Timer.AutoOffsetIncrement && Config.Timer.Offset > 1100)
								Config.Timer.Offset -= 100;

							//画像から取得
							var bitmap = SKBitmap.Decode(await response.Content.ReadAsStreamAsync());
							if (bitmap != null)
								using (bitmap)
									ProcessImage(bitmap, time);
						}
					}
					catch (TaskCanceledException ex)
					{
						DisplayWarningMessageUpdated.SendWarningMessage($"{time:HH:mm:ss} 画像ソース利用不可({ex.Message})");
					}
				}),
				Task.Run(async () =>
				{
					try
					{
						ApiResult<KyoshinMonitorLib.ApiResult.WebApi.Eew?> eewResult;
						if (OverrideSource != null)
						{
							var path = WebApiUrlGenerator.Generate(WebApiUrlType.EewJson, time).Replace("http://www.kmoni.bosai.go.jp/", "");
							var file = Path.Combine(OverrideSource, path);
							if (!File.Exists(file))
							{
								Logger.LogInfo($"{time:HH:mm:ss} EEWファイル {file} が見つかりません。リアルタイムに戻ります。");
								DisplayWarningMessageUpdated.SendWarningMessage($"{time:HH:mm:ss} EEWファイルが見つかりません。リアルタイムに戻ります。");
								OverrideDateTime = null;
								OverrideSource = null;
								return;
							}
							await using var stream = File.OpenRead(file);
							eewResult = new(HttpStatusCode.OK, await JsonSerializer.DeserializeAsync<KyoshinMonitorLib.ApiResult.WebApi.Eew>(stream));
						}
						else
							eewResult = await WebApi.GetEewInfo(time);

						// 新しい情報の場合のみ更新を通知する
						if (eewResult.Data?.ReportId != LatestEew?.ReportId ||
							eewResult.Data?.ReportNum > LatestEew?.ReportNum)
							EewController.Update(
								string.IsNullOrEmpty(eewResult.Data?.ReportId)
									? null
									: new KyoshinMonitorEew(eewResult.Data.ReportId)
									{
										Place = eewResult.Data.RegionName,
										IsCancelled = eewResult.Data.IsCancel ?? false,
										IsFinal = eewResult.Data.IsFinal ?? false,
										Count = eewResult.Data.ReportNum ?? 0,
										Depth = eewResult.Data.Depth ?? 0,
										Intensity = eewResult.Data.Calcintensity ?? JmaIntensity.Error,
										IsWarning = eewResult.Data.IsAlert,
										Magnitude = eewResult.Data.Magunitude ?? 0,
										OccurrenceTime = eewResult.Data.OriginTime ?? time,
										ReceiveTime = eewResult.Data.ReportTime ?? time,
										Location = eewResult.Data.Location,
										UpdatedTime = time,
									}, time);
						LatestEew = eewResult.Data;
					}
					catch (KyoshinMonitorException)
					{
						DisplayWarningMessageUpdated.SendWarningMessage($"{time:HH:mm:ss} EEWの情報が取得できませんでした。");
					}
				})
			]);
			RealtimeDataUpdated?.Invoke((time, Points, KyoshinEvents.ToArray()));

			trans.Finish(SpanStatus.Ok);
		}
		catch (TaskCanceledException ex)
		{
			DisplayWarningMessageUpdated.SendWarningMessage($"{time:HH:mm:ss} タイムアウトしました。");
			Logger.LogWarning(ex, "取得にタイムアウトしました。");
			trans.Finish(ex, SpanStatus.DeadlineExceeded);
		}
		catch (KyoshinMonitorException ex)
		{
			DisplayWarningMessageUpdated.SendWarningMessage($"{time:HH:mm:ss} {ex.Message}");
			Logger.LogWarning(ex, "取得にタイムアウトしました。");
			trans.Finish(ex, SpanStatus.DeadlineExceeded);
		}
		catch (HttpRequestException ex)
		{
			DisplayWarningMessageUpdated.SendWarningMessage($"{time:HH:mm:ss} HTTPエラー");
			Logger.LogWarning(ex, "HTTPエラー");
			trans.Finish(ex);
		}
		catch (Exception ex)
		{
			DisplayWarningMessageUpdated.SendWarningMessage($"{time:HH:mm:ss} 汎用エラー({ex.Message})");
			Logger.LogWarning(ex, "汎用エラー");
			trans.Finish(ex);
		}
		finally
		{
			IsRunning = false;
		}
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
					point.IsTmpDisabled || (point.NearPoints?.All(p => (latestIntensity - p.LatestIntensity ?? -3) >= 3) ?? true)
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
}

public class KyoshinMonitorEew(string id) : IEew
{
	public string Id { get; } = id;

	public string SourceDisplay => "強震モニタ";

	// みなしキャンセルを行うことがあるので setter も実装しておく
	public bool IsCancelled { get; set; }

	public bool IsTrueCancelled => false;

	public DateTime ReceiveTime { get; init; }

	public JmaIntensity Intensity { get; init; }

	public bool IsIntensityOver => false;

	public DateTime OccurrenceTime { get; init; }

	public string? Place { get; init; }

	public Location? Location { get; init; }

	public float? Magnitude { get; init; }

	public int Depth { get; init; }

	public int Count { get; init; }

	public bool IsWarning { get; init; }

	public bool IsFinal { get; init; }
	public bool IsAccuracyFound => LocationAccuracy != null && DepthAccuracy != null && MagnitudeAccuracy != null;
	public int? LocationAccuracy { get; set; } = null;
	public int? DepthAccuracy { get; set; } = null;
	public int? MagnitudeAccuracy { get; set; } = null;
	public bool? IsLocked { get; set; } = false;

	public Dictionary<int, JmaIntensity>? ForecastIntensityMap { get; set; }

	public int[]? WarningAreaCodes { get; set; }

	public string[]? WarningAreaNames { get; set; }

	// 精度フラグが存在しないので仮定震源要素で使用されるマジックナンバーかどうかを確認する
	/// <summary>
	/// 仮定震源要素か
	/// </summary>
	public bool IsTemporaryEpicenter => Depth == 10 && Magnitude is { } m && Math.Abs(m - 1.0) < 0.01;
	public int Priority => 0;

	/// <summary>
	/// 内部使用値
	/// </summary>
	public DateTime UpdatedTime { get; set; }

	public bool IsVisible { get; set; } = true;
}
