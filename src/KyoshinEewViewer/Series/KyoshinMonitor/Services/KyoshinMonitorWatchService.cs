using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;
using KyoshinEewViewer.Services;
using KyoshinMonitorLib;
using KyoshinMonitorLib.SkiaImages;
using KyoshinMonitorLib.UrlGenerator;
using MessagePack;
using Microsoft.Extensions.Logging;
using Sentry;
using SkiaSharp;
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
	private EewController EewControler { get; }
	private WebApi WebApi { get; set; }
	private RealtimeObservationPoint[]? Points { get; set; }

	/// <summary>
	/// タイムシフトなども含めた現在時刻
	/// </summary>
	public DateTime CurrentDisplayTime => LastElapsedDelayedTime + (DateTime.Now - LastElapsedDelayedLocalTime);
	private DateTime LastElapsedDelayedTime { get; set; }
	private DateTime LastElapsedDelayedLocalTime { get; set; }

	public DateTime? OverrideDateTime { get; set; }
	public string? OverrideSource { get; set; }


	public event Action<(DateTime time, RealtimeObservationPoint[] data, KyoshinEvent[] events)>? RealtimeDataUpdated;
	public event Action<DateTime>? RealtimeDataParseProcessStarted;

	public KyoshinMonitorWatchService(EewController eewControlService)
	{
		Logger = LoggingService.CreateLogger(this);
		EewControler = eewControlService;
		TimerService.Default.DelayedTimerElapsed += t => TimerElapsed(t);
		WebApi = new WebApi() { Timeout = TimeSpan.FromSeconds(2) };
	}

	public void Start()
	{
		DisplayWarningMessageUpdated.SendWarningMessage("走時表を初期化中...");

		var sw = Stopwatch.StartNew();
		Logger.LogInformation("走時表を準備しています。");
		TravelTimeTableService.Initalize();
		Logger.LogInformation("走時表を準備しました。 {Time}ms", sw.ElapsedMilliseconds);

		DisplayWarningMessageUpdated.SendWarningMessage("観測点情報を初期化中...");

		sw.Restart();
		Logger.LogInformation("観測点情報を読み込んでいます。");
		var points = MessagePackSerializer.Deserialize<ObservationPoint[]>(Properties.Resources.ShindoObsPoints, MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));
		Points = points.Where(p => p.Point != null && !p.IsSuspended).Select(p => new RealtimeObservationPoint(p)).ToArray();
		Logger.LogInformation("観測点情報を読み込みました。 {Time}ms", sw.ElapsedMilliseconds);

		foreach (var point in Points)
		{
			// 40キロ以内の近い順の最大9観測点を関連付ける
			point.NearPoints = Points
				.Where(p => point != p && point.Location.Distance(p.Location) < 40)
				.OrderBy(p => point.Location.Distance(p.Location))
				.Take(9)
				.ToArray();
		}

		TimerService.Default.StartMainTimer();
		DisplayWarningMessageUpdated.SendWarningMessage($"初回のデータ取得中です。しばらくお待ち下さい。");
	}

	private bool IsRunning { get; set; }
	private async void TimerElapsed(DateTime realTime)
	{
		// 観測点が読み込みできていなければ処理しない
		if (Points == null)
			return;

		var time = realTime;
		// リプレイ中であれば時刻を強制的に補正します
		if (OverrideDateTime is DateTime overrideDateTime)
		{
			time = overrideDateTime;
			OverrideDateTime = overrideDateTime.AddSeconds(1);
		}
		// タイムシフト中なら加算します(やっつけ)
		else if (ConfigurationService.Current.Timer.TimeshiftSeconds < 0)
			time = time.AddSeconds(ConfigurationService.Current.Timer.TimeshiftSeconds);

		LastElapsedDelayedTime = time;
		LastElapsedDelayedLocalTime = DateTime.Now;

		// 通信量制限モードが有効であればその間隔以外のものについては処理しない
		if (ConfigurationService.Current.KyoshinMonitor.FetchFrequency > 1
		 && (!EewControler.Found || !ConfigurationService.Current.KyoshinMonitor.ForcefetchOnEew)
		 && ((DateTimeOffset)time).ToUnixTimeSeconds() % ConfigurationService.Current.KyoshinMonitor.FetchFrequency != 0)
			return;

		// すでに処理中であれば戻る
		if (IsRunning)
			return;
		IsRunning = true;
		RealtimeDataParseProcessStarted?.Invoke(time);
		var trans = SentrySdk.StartTransaction("kyoshin-monitor", "process");
		try
		{
			try
			{
				if (OverrideSource != null)
				{
					var path = WebApiUrlGenerator.Generate(WebApiUrlType.RealtimeImg, time, RealtimeDataType.Shindo, false).Replace("http://www.kmoni.bosai.go.jp/", "");
					var file = Path.Combine(OverrideSource, path);
					if (!File.Exists(file))
					{
						Logger.LogInformation("{time:HH:mm:ss} 画像ファイル {file} が見つかりません。リアルタイムに戻ります。", time, file);
						DisplayWarningMessageUpdated.SendWarningMessage($"{time:HH:mm:ss} 画像ファイルが見つかりません。リアルタイムに戻ります。");
						OverrideDateTime = null;
						OverrideSource = null;
						return;
					}
					using var stream = File.OpenRead(file);
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
						if (ConfigurationService.Current.Timer.TimeshiftSeconds < 0)
						{
							DisplayWarningMessageUpdated.SendWarningMessage($"{time:HH:mm:ss} 利用できませんでした。({response.StatusCode})");
							return;
						}
						if (ConfigurationService.Current.Timer.AutoOffsetIncrement)
						{
							DisplayWarningMessageUpdated.SendWarningMessage($"{time:HH:mm:ss} オフセットを調整しました。");
							ConfigurationService.Current.Timer.Offset = Math.Min(5000, ConfigurationService.Current.Timer.Offset + 100);
							return;
						}

						DisplayWarningMessageUpdated.SendWarningMessage($"{time:HH:mm:ss} オフセットを調整してください。");
						return;
					}
					// オフセットが大きい場合1分に1回短縮を試みる
					if (time.Second == 0 && ConfigurationService.Current.Timer.AutoOffsetIncrement && ConfigurationService.Current.Timer.Offset > 1300)
						ConfigurationService.Current.Timer.Offset -= 100;

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
				return;
			}

			try
			{
				ApiResult<KyoshinMonitorLib.ApiResult.WebApi.Eew?> eewResult;
				if (OverrideSource != null)
				{
					var path = WebApiUrlGenerator.Generate(WebApiUrlType.EewJson, time).Replace("http://www.kmoni.bosai.go.jp/", "");
					var file = Path.Combine(OverrideSource, path);
					if (!File.Exists(file))
					{
						Logger.LogInformation("{time:HH:mm:ss} EEWファイル {file} が見つかりません。リアルタイムに戻ります。", time, file);
						DisplayWarningMessageUpdated.SendWarningMessage($"{time:HH:mm:ss} EEWファイルが見つかりません。リアルタイムに戻ります。");
						OverrideDateTime = null;
						OverrideSource = null;
						return;
					}
					using var stream = File.OpenRead(file);
					eewResult = new(HttpStatusCode.OK, await JsonSerializer.DeserializeAsync<KyoshinMonitorLib.ApiResult.WebApi.Eew>(stream));
				}
				else
					eewResult = await WebApi.GetEewInfo(time);

				EewControler.UpdateOrRefreshEew(
					string.IsNullOrEmpty(eewResult.Data?.ReportId) ? null : new Models.Eew(EewSource.NIED, eewResult.Data.ReportId)
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
						// PLUM法の場合
						IsUnreliableDepth = eewResult.Data.Depth == 10 && eewResult.Data.Magunitude == 1.0,
						IsUnreliableLocation = eewResult.Data.Depth == 10 && eewResult.Data.Magunitude == 1.0,
						IsUnreliableMagnitude = eewResult.Data.Depth == 10 && eewResult.Data.Magunitude == 1.0,
					}, time, ConfigurationService.Current.Timer.TimeshiftSeconds < 0);
			}
			catch (KyoshinMonitorException)
			{
				DisplayWarningMessageUpdated.SendWarningMessage($"{time:HH:mm:ss} EEWの情報が取得できませんでした。");
				Logger.LogWarning("EEWの情報が取得できませんでした。");
			}
			RealtimeDataUpdated?.Invoke((time, Points, KyoshinEvents.ToArray()));

			trans.Finish(SpanStatus.Ok);
		}
		catch (KyoshinMonitorException ex) when (ex.Message.Contains("Request Timeout"))
		{
			DisplayWarningMessageUpdated.SendWarningMessage($"{time:HH:mm:ss} タイムアウトしました。");
			Logger.LogWarning("取得にタイムアウトしました。");
			trans.Finish(ex, SpanStatus.DeadlineExceeded);
		}
		catch (KyoshinMonitorException ex)
		{
			DisplayWarningMessageUpdated.SendWarningMessage($"{time:HH:mm:ss} {ex.Message}");
			Logger.LogWarning("取得にタイムアウトしました。");
			trans.Finish(ex, SpanStatus.DeadlineExceeded);
		}
		catch (HttpRequestException ex)
		{
			DisplayWarningMessageUpdated.SendWarningMessage($"{time:HH:mm:ss} HTTPエラー");
			Logger.LogWarning("HTTPエラー\n{Message}", ex.Message);
			trans.Finish(ex);
		}
		catch (Exception ex)
		{
			DisplayWarningMessageUpdated.SendWarningMessage($"{time:HH:mm:ss} 汎用エラー({ex.Message})");
			Logger.LogWarning("汎用エラー\n{ex}", ex);
			trans.Finish(ex);
		}
		finally
		{
			IsRunning = false;
		}
	}

	private List<KyoshinEvent> KyoshinEvents { get; } = new();
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

		// イベントチェック
		foreach (var point in Points)
		{
			if (point.IntensityDiff < 1.1)
			{
				// 未来もしくは過去のイベントは離脱
				if (point.Event is KyoshinEvent evt && (point.EventedAt > time || point.EventedExpireAt < time))
				{
					point.Event = null;
					evt.RemovePoint(point);

					if (evt.PointCount <= 0)
						KyoshinEvents.Remove(evt);
				}
				continue;
			}
			// 周囲の観測点が未計算の場合戻る
			if (point.NearPoints == null)
				continue;

			// 有効な周囲の観測点の数
			var availableNearCount = point.NearPoints.Count(n => n.HasValidHistory);

			// 周囲の観測点が存在しない場合 3 以上でeventedとしてマーク
			if (availableNearCount == 0)
			{
				if (point.IntensityDiff >= 2 && point.Event == null)
				{
					point.Event = new(time, point);
					point.EventedAt = time;
					KyoshinEvents.Add(point.Event);
				}
				continue;
			}

			// 周囲の観測点の 1/2 以上 0.5 であればEventedとしてマーク
			var events = new List<KyoshinEvent>();
			if (point.Event != null)
				events.Add(point.Event);
			var count = 0;
			var threshold = Math.Max(availableNearCount / 2, 1);
			foreach (var np in point.NearPoints)
			{
				if (np.IntensityDiff >= 0.5)
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
				}

				// マージしたイベントと異なる状態だった場合追加
				if (point.Event != firstEvent)
					firstEvent.AddPoint(point, time);
				continue;
			}
			// 1件の場合はイベントに追加
			if (uniqueEvents.Any())
			{
				events[0].AddPoint(point, time);
				continue;
			}

			if (point.Event == null)
			{
				point.Event = new(time, point);
				KyoshinEvents.Add(point.Event);
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
			}
		}
	}
}
