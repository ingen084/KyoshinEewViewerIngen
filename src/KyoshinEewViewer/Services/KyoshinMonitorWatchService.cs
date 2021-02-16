using KyoshinEewViewer.Models.Events;
using KyoshinMonitorLib;
using KyoshinMonitorLib.Images;
using MessagePack;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services
{
	public class KyoshinMonitorWatchService
	{
		private WebApi WebApi { get; set; }
		private ObservationPoint[] Points { get; set; }
		private ImageAnalysisResult[] ResultCache { get; set; }

		private EewControlService EewControlService { get; }
		private LoggerService Logger { get; }
		private ConfigurationService ConfigService { get; }
		private TravelTimeTableService TrTimeTableService { get; }
		private TimerService TimerService { get; }

		private RealtimeDataUpdated RealtimeDataUpdatedEvent { get; }
		private RealtimeDataParseProcessStarted RealtimeDataParseProcessStartedEvent { get; }

		public KyoshinMonitorWatchService(
			LoggerService logger,
			EewControlService eewControlService,
			TravelTimeTableService trTimeTableService,
			ConfigurationService configService,
			TimerService timeService,
			IEventAggregator aggregator)
		{
			Logger = logger;
			EewControlService = eewControlService;
			ConfigService = configService;
			TrTimeTableService = trTimeTableService;
			TimerService = timeService;

			RealtimeDataUpdatedEvent = aggregator.GetEvent<RealtimeDataUpdated>();
			RealtimeDataParseProcessStartedEvent = aggregator.GetEvent<RealtimeDataParseProcessStarted>();

			// asyncによる待機を行うのでEventAggregatorは使用できない
			TimerService.MainTimerElapsed += TimerElapsed;
		}

		public async void Start()
		{
			Logger.OnWarningMessageUpdated("初期化中...");
			Logger.Info("観測点情報を読み込んでいます。");
			var points = MessagePackSerializer.Deserialize<ObservationPoint[]>(Properties.Resources.ShindoObsPoints, MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));
			WebApi = new WebApi() { Timeout = TimeSpan.FromSeconds(2) };
			Points = points;

			Logger.Info("走時表を準備しています。");
			TrTimeTableService.Initalize();

			await TimerService.StartMainTimerAsync();
			Logger.OnWarningMessageUpdated($"初回のデータ取得中です。しばらくお待ち下さい。");
		}

		private async Task TimerElapsed(DateTime realTime)
		{
			var time = realTime;
			// タイムシフト中なら加算します(やっつけ)
			if (ConfigService.Configuration.Timer.TimeshiftSeconds < 0)
				time = time.AddSeconds(ConfigService.Configuration.Timer.TimeshiftSeconds);

			// 通信量制限モードが有効であればその間隔以外のものについては処理しない
			if (ConfigService.Configuration.KyoshinMonitor.FetchFrequency > 1
			 && (EewControlService.Found || !ConfigService.Configuration.KyoshinMonitor.ForcefetchOnEew)
			 && ((DateTimeOffset)time).ToUnixTimeSeconds() % ConfigService.Configuration.KyoshinMonitor.FetchFrequency != 0)
				return;

			RealtimeDataParseProcessStartedEvent.Publish(time);
			try
			{
				var eventData = new RealtimeDataUpdated { Time = time };

				try
				{
					//失敗したら画像から取得
					var result = ResultCache == null ?
						await WebApi.ParseScaleFromParameterAsync(Points, time) :
						await WebApi.ParseScaleFromParameterAsync(ResultCache, time);
					if (result?.StatusCode != System.Net.HttpStatusCode.OK)
					{
						if (ConfigService.Configuration.Timer.TimeshiftSeconds < 0)
						{
							Logger.OnWarningMessageUpdated($"{time:HH:mm:ss} 利用できませんでした。({result?.StatusCode})");
							return;
						}
						if (ConfigService.Configuration.Timer.AutoOffsetIncrement)
						{
							Logger.OnWarningMessageUpdated($"{time:HH:mm:ss} オフセットを調整しました。");
							ConfigService.Configuration.Timer.Offset = Math.Min(5000, ConfigService.Configuration.Timer.Offset + 100);
							return;
						}

						Logger.OnWarningMessageUpdated($"{time:HH:mm:ss} オフセットを調整してください。");
						return;
					}
					ResultCache = eventData.Data = result.Data.ToArray();
				}
				catch (KyoshinMonitorException ex)
				{
					Logger.OnWarningMessageUpdated($"{time:HH:mm:ss} 画像ソース利用不可({ex.Message})");
					return;
				}

				try
				{
					var eewResult = await WebApi.GetEewInfo(time);

					EewControlService.UpdateOrRefreshEew(
						string.IsNullOrEmpty(eewResult.Data?.CalcintensityString) ? null : new Models.Eew
						{
							Id = eewResult.Data.ReportId,
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
				}
				catch (KyoshinMonitorException)
				{
					Logger.OnWarningMessageUpdated($"{time:HH:mm:ss} EEWの情報が取得できませんでした。");
					Logger.Warning("EEWの情報が取得できませんでした。");
				}
				RealtimeDataUpdatedEvent.Publish(eventData);
			}
			catch (KyoshinMonitorException ex) when (ex.Message.Contains("Request Timeout"))
			{
				Logger.OnWarningMessageUpdated($"{time:HH:mm:ss} タイムアウトしました。");
				Logger.Warning("取得にタイムアウトしました。");
			}
			catch (KyoshinMonitorException ex)
			{
				Logger.OnWarningMessageUpdated($"{time:HH:mm:ss} {ex.Message}");
				Logger.Warning("取得にタイムアウトしました。");
			}
			catch (Exception ex)
			{
				Logger.OnWarningMessageUpdated($"{time:HH:mm:ss} 汎用エラー({ex.Message})");
				Logger.Warning("汎用エラー\n" + ex);
			}
		}
	}
}