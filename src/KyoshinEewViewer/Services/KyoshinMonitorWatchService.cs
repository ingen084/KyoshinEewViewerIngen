using KyoshinMonitorLib;
using KyoshinMonitorLib.Images;
using KyoshinMonitorLib.Timers;
using KyoshinMonitorLib.UrlGenerator;
using MessagePack;
using Prism.Events;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services
{
	public class KyoshinMonitorWatchService
	{
		// TODO: MainTimerとNetworkTimeServiceを融合させる
		private SecondBasedTimer MainTimer { get; }
		private AppApi AppApi { get; set; }
		private WebApi WebApi { get; set; }
		private ObservationPoint[] Points { get; set; }

		private LoggerService Logger { get; }
		private ConfigurationService ConfigService { get; }
		private TrTimeTableService TrTimeTableService { get; }

		private Timer UpdateOffsetTimer { get; }

		private Events.RealTimeDataUpdated RealTimeDataUpdatedEvent { get; }
		private Events.TimeElapsed TimeElapsedEvent { get; }
		public KyoshinMonitorWatchService(
			LoggerService logger,
			TrTimeTableService trTimeTableService,
			ConfigurationService configService,
			NetworkTimeService networkTimeService,
			IEventAggregator aggregator)
		{
			Logger = logger;
			ConfigService = configService;
			TrTimeTableService = trTimeTableService;

			RealTimeDataUpdatedEvent = aggregator.GetEvent<Events.RealTimeDataUpdated>();
			TimeElapsedEvent = aggregator.GetEvent<Events.TimeElapsed>();
			aggregator.GetEvent<Events.TimeSynced>().Subscribe(t => MainTimer.UpdateTime(t));

			MainTimer = new SecondBasedTimer()
			{
				Offset = TimeSpan.FromMilliseconds(ConfigService.Configuration.Offset),
			};
			UpdateOffsetTimer = new Timer(s => MainTimer.Offset = TimeSpan.FromMilliseconds(ConfigService.Configuration.Offset));
			ConfigService.Configuration.ConfigurationUpdated += n =>
			{
				if (n == nameof(ConfigService.Configuration.Offset))
					UpdateOffsetTimer.Change(1000, Timeout.Infinite);
			};
			MainTimer.Elapsed += TimerElapsed;

			Task.Run(async () =>
			{
				// TODO: ViewModelから呼び出す形にする
				// 現状このウェイトがないと初期化前に呼び出してしまう(あたりまえ
				await Task.Delay(1000);

				Logger.OnWarningMessageUpdated("初期化中...");
				Logger.Info("観測点情報を読み込んでいます。");
				var points = LZ4MessagePackSerializer.Deserialize<ObservationPoint[]>(Properties.Resources.ShindoObsPoints);
				WebApi = new WebApi() { Timeout = TimeSpan.FromSeconds(2) };
				AppApi = new AppApi(points) { Timeout = TimeSpan.FromSeconds(2) };
				Points = points;

				Logger.Info("走時表を準備しています。");
				TrTimeTableService.Initalize();

				Logger.Info("初回の時刻同期をしています。");
				var time = await networkTimeService.GetNowTimeAsync();
				MainTimer.Start(time ?? DateTime.Now);
				Logger.Info("メインタイマー開始");
				Logger.OnWarningMessageUpdated($"初回のデータ取得中です。しばらくお待ち下さい。");
			}).ConfigureAwait(false);
		}

		private async Task TimerElapsed(DateTime time)
		{
			try
			{
				TimeElapsedEvent.Publish(time);
				var eventData = new Events.RealTimeDataUpdated { Time = time };

				async Task<bool> ParseUseImage()
				{
					try
					{
						//失敗したら画像から取得
						var result = await WebApi.ParseIntensityFromParameterAsync(Points, time);
						if (result?.StatusCode != System.Net.HttpStatusCode.OK)
						{
							Logger.OnWarningMessageUpdated($"{time.ToString("HH:mm:ss")} オフセットを調整してください。");
							return false;
						}
						eventData.Data = result.Data.Where(r => r.AnalysisResult != null).Select(r => new LinkedRealTimeData(new LinkedObservationPoint(null, r.ObservationPoint), r.AnalysisResult)).ToArray();
						eventData.IsUseAlternativeSource = true;
						//Debug.WriteLine("Image Count: " + result.Data.Count(d => d.AnalysisResult != null));
					}
					catch (KyoshinMonitorException ex)
					{
						Logger.OnWarningMessageUpdated($"{time.ToString("HH:mm:ss")} 画像ソース利用不可({ex.Message})");
						return false;
					}
					return true;
				}

				if (ConfigService.Configuration.AlwaysUseImageParseMode)
					await ParseUseImage();
				else
				{
					//APIで取得
					var shindoResult = await AppApi.GetLinkedRealTimeData(time, RealTimeDataType.Shindo);
					if (shindoResult?.Data != null)
					{
						eventData.Data = shindoResult.Data;
						eventData.IsUseAlternativeSource = false;
						//Debug.WriteLine("API Count: " + shindoResult.Data.Count(d => d.Value != null));
					}
					else if (ConfigService.Configuration.UseImageParseMode)
						await ParseUseImage();
					else
						Logger.OnWarningMessageUpdated($"{time.ToString("HH:mm:ss")} オフセット調整または画像を利用してください。");
				}

				try
				{
					var eewResult = await WebApi.GetEewInfo(time);
					if (!string.IsNullOrEmpty(eewResult.Data?.CalcintensityString))
					{
						eventData.Eews = new Models.Eew[] {new Models.Eew
						{
							Place = eewResult.Data.RegionName,
							IsCancelled = eewResult.Data.IsCancel ?? false,
							IsFinal = eewResult.Data.IsFinal ?? false,
							Count = eewResult.Data.ReportNum ?? 0,
							Depth = eewResult.Data.Depth ?? 0,
							Intensity = eewResult.Data.Calcintensity,
							IsWarning = eewResult.Data.IsAlert,
							Magnitude = eewResult.Data.Magunitude ?? 0,
							OccurrenceTime = eewResult.Data.OriginTime ?? DateTime.Now,
							ReceiveTime = eewResult.Data.ReportTime ?? DateTime.Now,
							Location = eewResult.Data.Location,
						}};
					}
				}
				catch (KyoshinMonitorException)
				{
					Logger.OnWarningMessageUpdated($"{time.ToString("HH:mm:ss")} EEWの情報が取得できませんでした。");
					Logger.Warning("EEWの情報が取得できませんでした。");
				}
				RealTimeDataUpdatedEvent.Publish(eventData);
			}
			catch (KyoshinMonitorException ex) when (ex.Message.Contains("Request Timeout"))
			{
				Logger.OnWarningMessageUpdated($"{time.ToString("HH:mm:ss")} タイムアウトしました。");
				Logger.Warning("取得にタイムアウトしました。");
			}
			catch (KyoshinMonitorException ex)
			{
				Logger.OnWarningMessageUpdated($"{time.ToString("HH:mm:ss")} {ex.Message}");
				Logger.Warning("取得にタイムアウトしました。");
			}
			catch (Exception ex)
			{
				Logger.OnWarningMessageUpdated($"{time.ToString("HH:mm:ss")} 汎用エラー({ex.Message})");
				Logger.Warning("汎用エラー\n" + ex);
			}
		}
	}
}