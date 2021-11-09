using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;
using KyoshinEewViewer.Services;
using KyoshinMonitorLib;
using KyoshinMonitorLib.SkiaImages;
using MessagePack;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Services;

public class KyoshinMonitorWatchService
{
	private ILogger Logger { get; }
	private EewControlService EewControler { get; }
	private WebApi WebApi { get; set; }
	private ObservationPoint[] Points { get; set; }
	private ImageAnalysisResult[]? ResultCache { get; set; }

	public event Action<(DateTime time, ImageAnalysisResult[] data)>? RealtimeDataUpdated;
	public event Action<DateTime>? RealtimeDataParseProcessStarted;

	public KyoshinMonitorWatchService(EewControlService eewControlService)
	{
		Logger = LoggingService.CreateLogger(this);
		EewControler = eewControlService;
		TimerService.Default.DelayedTimerElapsed += t => TimerElapsed(t);
		WebApi = new WebApi() { Timeout = TimeSpan.FromSeconds(2) };
		Logger.LogInformation("観測点情報を読み込んでいます。");
		var sw = Stopwatch.StartNew();
		var points = MessagePackSerializer.Deserialize<ObservationPoint[]>(Properties.Resources.ShindoObsPoints, MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));
		Points = points;
		Logger.LogInformation("観測点情報を読み込みました。 {Time}ms", sw.ElapsedMilliseconds);
	}

	public void Start()
	{
		DisplayWarningMessageUpdated.SendWarningMessage("初期化中...");
		Logger.LogInformation("走時表を準備しています。");
		TravelTimeTableService.Initalize();

		TimerService.Default.StartMainTimer();
		DisplayWarningMessageUpdated.SendWarningMessage($"初回のデータ取得中です。しばらくお待ち下さい。");
	}

	private async void TimerElapsed(DateTime realTime)
	{
		var time = realTime;
		// タイムシフト中なら加算します(やっつけ)
		if (ConfigurationService.Current.Timer.TimeshiftSeconds < 0)
			time = time.AddSeconds(ConfigurationService.Current.Timer.TimeshiftSeconds);

		// 通信量制限モードが有効であればその間隔以外のものについては処理しない
		if (ConfigurationService.Current.KyoshinMonitor.FetchFrequency > 1
		 && (!EewControler.Found || !ConfigurationService.Current.KyoshinMonitor.ForcefetchOnEew)
		 && ((DateTimeOffset)time).ToUnixTimeSeconds() % ConfigurationService.Current.KyoshinMonitor.FetchFrequency != 0)
			return;

		RealtimeDataParseProcessStarted?.Invoke(time);
		try
		{
			try
			{
				//画像から取得
				var result = ResultCache == null ?
					await WebApi.ParseScaleFromParameterAsync(Points, time) :
					await WebApi.ParseScaleFromParameterAsync(ResultCache, time);
				if (result?.StatusCode != System.Net.HttpStatusCode.OK)
				{
					if (ConfigurationService.Current.Timer.TimeshiftSeconds < 0)
					{
						DisplayWarningMessageUpdated.SendWarningMessage($"{time:HH:mm:ss} 利用できませんでした。({result?.StatusCode})");
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
				ResultCache = result.Data?.ToArray() ?? throw new Exception("{time:HH:mm:ss} 取得失敗");
			}
			catch (KyoshinMonitorException ex)
			{
				DisplayWarningMessageUpdated.SendWarningMessage($"{time:HH:mm:ss} 画像ソース利用不可({ex.Message})");
				return;
			}

			try
			{
				var eewResult = await WebApi.GetEewInfo(time);

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
			RealtimeDataUpdated?.Invoke((time, ResultCache));
		}
		catch (KyoshinMonitorException ex) when (ex.Message.Contains("Request Timeout"))
		{
			DisplayWarningMessageUpdated.SendWarningMessage($"{time:HH:mm:ss} タイムアウトしました。");
			Logger.LogWarning("取得にタイムアウトしました。");
		}
		catch (KyoshinMonitorException ex)
		{
			DisplayWarningMessageUpdated.SendWarningMessage($"{time:HH:mm:ss} {ex.Message}");
			Logger.LogWarning("取得にタイムアウトしました。");
		}
		catch (HttpRequestException ex)
		{
			DisplayWarningMessageUpdated.SendWarningMessage($"{time:HH:mm:ss} HTTPエラー");
			Logger.LogWarning("HTTPエラー\n{Message}", ex.Message);
		}
		catch (Exception ex)
		{
			DisplayWarningMessageUpdated.SendWarningMessage($"{time:HH:mm:ss} 汎用エラー({ex.Message})");
			Logger.LogWarning("汎用エラー\n{ex}", ex);
		}
	}
}
