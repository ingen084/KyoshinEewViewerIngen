using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;
using KyoshinEewViewer.Services;
using KyoshinMonitorLib;
using KyoshinMonitorLib.Images;
using MessagePack;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Services
{
	public class KyoshinMonitorWatchService
	{
		private static KyoshinMonitorWatchService? _default;
		public static KyoshinMonitorWatchService Default => _default ??= new();

		private WebApi WebApi { get; set; }
		private ObservationPoint[] Points { get; set; }
		private ImageAnalysisResult[]? ResultCache { get; set; }

		public KyoshinMonitorWatchService()
		{
			MessageBus.Current.Listen<DelayedTimeElapsed>().Subscribe(t => TimerElapsed(t.Time));
			WebApi = new WebApi() { Timeout = TimeSpan.FromSeconds(2) };
			Trace.TraceInformation("観測点情報を読み込んでいます。");
			var sw = Stopwatch.StartNew();
			var points = MessagePackSerializer.Deserialize<ObservationPoint[]>(Properties.Resources.ShindoObsPoints, MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));
			Points = points;
			Trace.TraceInformation($"観測点情報を読み込みました。 {sw.ElapsedMilliseconds}ms");
		}

		public void Start()
		{
			DisplayWarningMessageUpdated.SendWarningMessage("初期化中...");
			Trace.TraceInformation("走時表を準備しています。");
			TravelTimeTableService.Initalize();

			TimerService.Default.StartMainTimer();
			DisplayWarningMessageUpdated.SendWarningMessage($"初回のデータ取得中です。しばらくお待ち下さい。");
		}

		private async void TimerElapsed(DateTime realTime)
		{
			var time = realTime;
			// タイムシフト中なら加算します(やっつけ)
			if (ConfigurationService.Default.Timer.TimeshiftSeconds < 0)
				time = time.AddSeconds(ConfigurationService.Default.Timer.TimeshiftSeconds);

			// 通信量制限モードが有効であればその間隔以外のものについては処理しない
			if (ConfigurationService.Default.KyoshinMonitor.FetchFrequency > 1
			 && (!EewControlService.Default.Found || !ConfigurationService.Default.KyoshinMonitor.ForcefetchOnEew)
			 && ((DateTimeOffset)time).ToUnixTimeSeconds() % ConfigurationService.Default.KyoshinMonitor.FetchFrequency != 0)
				return;

			MessageBus.Current.SendMessage(new RealtimeDataParseProcessStarted(time));
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
						if (ConfigurationService.Default.Timer.TimeshiftSeconds < 0)
						{
							DisplayWarningMessageUpdated.SendWarningMessage($"{time:HH:mm:ss} 利用できませんでした。({result?.StatusCode})");
							return;
						}
						if (ConfigurationService.Default.Timer.AutoOffsetIncrement)
						{
							DisplayWarningMessageUpdated.SendWarningMessage($"{time:HH:mm:ss} オフセットを調整しました。");
							ConfigurationService.Default.Timer.Offset = Math.Min(5000, ConfigurationService.Default.Timer.Offset + 100);
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

					EewControlService.Default.UpdateOrRefreshEew(
						string.IsNullOrEmpty(eewResult.Data?.ReportId) ? null : new Core.Models.Eew(EewSource.NIED, eewResult.Data.ReportId)
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
				}
				catch (KyoshinMonitorException)
				{
					DisplayWarningMessageUpdated.SendWarningMessage($"{time:HH:mm:ss} EEWの情報が取得できませんでした。");
					Trace.TraceWarning("EEWの情報が取得できませんでした。");
				}
				MessageBus.Current.SendMessage(new RealtimeDataUpdated(time, ResultCache));
			}
			catch (KyoshinMonitorException ex) when (ex.Message.Contains("Request Timeout"))
			{
				DisplayWarningMessageUpdated.SendWarningMessage($"{time:HH:mm:ss} タイムアウトしました。");
				Trace.TraceWarning("取得にタイムアウトしました。");
			}
			catch (KyoshinMonitorException ex)
			{
				DisplayWarningMessageUpdated.SendWarningMessage($"{time:HH:mm:ss} {ex.Message}");
				Trace.TraceWarning("取得にタイムアウトしました。");
			}
			catch (HttpRequestException ex)
			{
				DisplayWarningMessageUpdated.SendWarningMessage($"{time:HH:mm:ss} HTTPエラー");
				Trace.TraceWarning("HTTPエラー\n" + ex.Message);
			}
			catch (Exception ex)
			{
				DisplayWarningMessageUpdated.SendWarningMessage($"{time:HH:mm:ss} 汎用エラー({ex.Message})");
				Trace.TraceWarning("汎用エラー\n" + ex);
			}
		}
	}
}
