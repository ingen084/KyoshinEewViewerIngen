using KyoshinEewViewer.Dmdata;
using KyoshinEewViewer.Dmdata.ApiResponses;
using KyoshinEewViewer.Dmdata.Exceptions;
using KyoshinEewViewer.Models;
using KyoshinEewViewer.Models.Events;
using KyoshinMonitorLib;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace KyoshinEewViewer.Services
{
	public class DmdataService
	{

		public List<Earthquake> Earthquakes { get; } = new List<Earthquake>();
		private EarthquakeUpdated EarthquakeUpdated { get; }

		public BillingResponse BillingInfo { get; private set; }
		public DmdataBillingInfoUpdated BillingInfoUpdated { get; }
		public bool IgnoreBillingstatusCheck { get; private set; }

		private DmdataStatus status = 0;
		public DmdataStatus Status
		{
			get => status;
			set
			{
				status = value;
				StatusUpdated.Publish();
			}
		}
		public bool Available => Status switch
		{
			DmdataStatus.Stopping => false,
			DmdataStatus.Failed => false,
			DmdataStatus.StoppingForInvalidKey => false,
			DmdataStatus.Initalizing => true,
			DmdataStatus.UsingPullForForbidden => true,
			DmdataStatus.UsingPullForError => true,
			DmdataStatus.UsingPull => true,
			DmdataStatus.ReconnectingWebSocket => true,
			DmdataStatus.UsingWebSocket => true,
			_ => false,
		};
		public DmdataStatusUpdated StatusUpdated { get; }

		public Timer UpdateBillingStatusTimer { get; }
		public Timer PullingTimer { get; }

		private Random Random { get; } = new Random();
		private DmdataApiClient ApiClient { get; }
		private DmdataSocket DmdataSocket { get; set; }
		private LoggerService Logger { get; }
		private ConfigurationService ConfigService { get; }

		/// <summary>
		/// telegram.listで使用するAPI
		/// </summary>
		private int NewCatch { get; set; }

		private XmlSerializer ReportSerializer { get; } = new XmlSerializer(typeof(Report));
		private readonly string[] ParseTitles = { "震度速報", "震源に関する情報", "震源・震度に関する情報" };

		public DmdataService(ConfigurationService configService, LoggerService logger, IEventAggregator eventAggregator)
		{
			ConfigService = configService;
			Logger = logger;
			ApiClient = new DmdataApiClient(ConfigService.Configuration.Dmdata.ApiKey);

			EarthquakeUpdated = eventAggregator.GetEvent<EarthquakeUpdated>();
			BillingInfoUpdated = eventAggregator.GetEvent<DmdataBillingInfoUpdated>();
			StatusUpdated = eventAggregator.GetEvent<DmdataStatusUpdated>();

			ConfigService.Configuration.Dmdata.PropertyChanged += async (s, e) =>
			{
				switch (e.PropertyName)
				{
					case nameof(ConfigService.Configuration.Dmdata.ApiKey):
						Logger.Info("dmdataのAPIキーが更新されました");
						ApiClient.ApiKey = ConfigService.Configuration.Dmdata.ApiKey;

						await InitalizeAsync().ConfigureAwait(false);
						break;
					case nameof(ConfigService.Configuration.Dmdata.UseWebSocket):
						Logger.Info("WebSocketの接続がトグルされました");
						await InitalizeAsync().ConfigureAwait(false);
						break;
				}
			};

			UpdateBillingStatusTimer = new Timer(async s => await UpdateBillingStatusAsync(), null, TimeSpan.FromSeconds(1), Timeout.InfiniteTimeSpan);
			PullingTimer = new Timer(async s => await PullXmlAsync(false), null, Timeout.Infinite, Timeout.Infinite);
		}

		public async Task InitalizeAsync()
		{
			// セットしていない場合停止中に
			if (string.IsNullOrWhiteSpace(ConfigService.Configuration.Dmdata.ApiKey))
			{
				Logger.Info("APIキーが存在しないためdmdataは利用しません");
				// WebSocketに接続していたら切断
				if (DmdataSocket != null)
					await DmdataSocket.DisconnectAsync();
				Status = DmdataStatus.Stopping;
				BillingInfo = null;
				BillingInfoUpdated?.Publish();
				return;
			}
			// フラグのリセット
			IgnoreBillingstatusCheck = false;
			await UpdateBillingStatusAsync();
			// 初回データのDL+WebSocketに接続するまでは初期化状態
			Status = DmdataStatus.Initalizing;
			await PullXmlAsync(true);
			// APIキー変更でWebSocketに再接続を行う
			if (DmdataSocket != null)
				await DmdataSocket.DisconnectAsync();
			await TryConnectWebSocketAsync();
		}

		private async Task PullXmlAsync(bool firstSync)
		{
			// PULLモードでない場合無視
			if (Status != DmdataStatus.UsingPull && Status != DmdataStatus.UsingPullForForbidden && Status != DmdataStatus.UsingPullForError && Status != DmdataStatus.Initalizing)
				return;

			try
			{
				// 初回取得は震源震度に関する情報だけにしておく
				var resp = await ApiClient.GetTelegramListAsync(type: firstSync ? "VXSE53" : "VXSE5", xml: true, newCatch: NewCatch, limit: 5);
				NewCatch = resp.NewCatch;

				// TODO: リトライ処理の実装
				if (resp.Status != "ok")
				{
					Status = DmdataStatus.Failed;
					Logger.Info($"dmdataからのリストの取得に失敗しました status: {resp.Status}, errorMessage: {resp.Error?.Message}");
					return;
				}
				Logger.Info($"dmdata items: " + resp.Items.Length);
				foreach (var item in resp.Items)
				{
					// 解析すべき情報だけ取ってくる
					if (!ParseTitles.Contains(item.XmlData.Control.Title))
						continue;

					Logger.Info("dmdataから取得しています: " + item.Key);
					using var rstr = await ApiClient.GetTelegramStreamAsync(item.Key);
					var report = (Report)ReportSerializer.Deserialize(rstr);

					ProcessReport(report, firstSync);
				}

				if (firstSync)
					EarthquakeUpdated.Publish(null);

				// 設定値の1～1.2倍のランダム間隔でリクエストを行う
				PullingTimer.Change(TimeSpan.FromSeconds(ConfigService.Configuration.Dmdata.PullInterval * (1 + Random.NextDouble() * .2)), Timeout.InfiniteTimeSpan);
			}
			catch (DmdataForbiddenException ex)
			{
				Logger.Error("必須APIを利用する権限がないもしくはAPIキーが不正です\n" + ex);
				Status = DmdataStatus.StoppingForInvalidKey;
			}
		}
		private void ProcessReport(Report report, bool firstSync)
		{
			// TODO: EventIdの異なる電文に対応する
			var eq = Earthquakes.FirstOrDefault(e => e.Id == report.Head.EventID);
			if (eq == null)
			{
				eq = new Earthquake
				{
					Id = report.Head.EventID,
					IsSokuhou = true,
					Intensity = JmaIntensity.Unknown
				};
				if (firstSync)
					Earthquakes.Add(eq);
				else
					Earthquakes.Insert(0, eq);
			}

			switch (report.Control.Title)
			{
				case "震度速報":
					{
						if (!eq.IsSokuhou)
							break;
						eq.IsSokuhou = true;
						eq.OccurrenceTime = report.Head.TargetDateTime;
						eq.IsReportTime = true;

						var infoItem = report.Head.Headline.Informations.First().Items.First();
						eq.Intensity = infoItem.Kind.Name.Replace("震度", "").ToJmaIntensity();
						eq.Place = infoItem.Areas.Area.First().Name;
						break;
					}
				case "震源に関する情報":
					{
						eq.IsSokuhou = false;
						eq.OccurrenceTime = report.Body.Earthquake.OriginTime;
						eq.IsReportTime = false;

						eq.Place = report.Body.Earthquake.Hypocenter.Area.Name;
						eq.Magnitude = report.Body.Earthquake.Magnitude.Value;
						eq.Depth = report.Body.Earthquake.Hypocenter.Area.Coordinate.GetDepth() ?? -1;
						break;
					}
				case "震源・震度に関する情報":
					{
						eq.IsSokuhou = false;
						eq.OccurrenceTime = report.Body.Earthquake.OriginTime;
						eq.IsReportTime = false;

						eq.Intensity = report.Body.Intensity?.Observation?.MaxInt?.ToJmaIntensity() ?? JmaIntensity.Unknown;
						eq.Place = report.Body.Earthquake.Hypocenter.Area.Name;
						eq.Magnitude = report.Body.Earthquake.Magnitude.Value;
						eq.Depth = report.Body.Earthquake.Hypocenter.Area.Coordinate.GetDepth() ?? -1;
						break;
					}
				default:
					Logger.Error("不明なTitleをパースしました。: " + report.Control.Title);
					break;
			}
			if (!firstSync)
				EarthquakeUpdated.Publish(eq);
		}

		/// <summary>
		/// 可能であればWebSocketへの接続を行い、ステータスの更新を行う
		/// </summary>
		/// <returns></returns>
		private async Task TryConnectWebSocketAsync()
		{
			// セットしていない場合停止中に
			if (string.IsNullOrWhiteSpace(ConfigService.Configuration.Dmdata.ApiKey))
			{
				Logger.Info("APIキーが存在しないためdmdataは利用しません");
				Status = DmdataStatus.Stopping;
				BillingInfo = null;
				BillingInfoUpdated?.Publish();
				return;
			}
			// WebSocketを利用しない場合
			if (!ConfigService.Configuration.Dmdata.UseWebSocket)
			{
				Logger.Info("WebSocketを利用しない設定になっています");
				Status = DmdataStatus.UsingPull;
				await PullXmlAsync(false);
				return;
			}
			// 切断されていた場合は無視する
			if (Status == DmdataStatus.UsingPullForError)
				return;

			try
			{
				if (DmdataSocket?.IsConnected ?? false)
				{
					Logger.Warning("すでにWebSocketに接続中でした");
					return;
				}

				if (DmdataSocket != null)
				{
					DmdataSocket.Dispose();
					DmdataSocket = null;
				}
				DmdataSocket = new DmdataSocket(ApiClient);
				DmdataSocket.Connected += (s, e) =>
				{
					Logger.Info("WebSocketに接続完了しました " + e.Type);
					Status = DmdataStatus.UsingWebSocket;
				};
				DmdataSocket.ConnectionFull += (s, e) => 
				{
					Status = DmdataStatus.UsingPullForError;
				};
				DmdataSocket.Disconnected += async (s, e) =>
				{
					Logger.Info("WebSocketから切断されました");
					// 再接続を試みる,接続不可の場合自動でキャンセル、ステータスの更新が行われる
					await TryConnectWebSocketAsync();
				};
				DmdataSocket.Error += async (s, e) =>
				{
					switch (e.Code)
					{
						// 手動での切断 or 契約終了の場合はPULL型に変更して切断
						case "socket.end":
						case "contract.end":
							Status = DmdataStatus.UsingPullForError;
							await DmdataSocket.DisconnectAsync();
							return;
					}
					// それ以外の場合かつ切断された場合は再接続を試みる
					if (e.Action == "close")
						await TryConnectWebSocketAsync();
				};
				DmdataSocket.DataReceived += (s, e) =>
				{
					if (!e.Validate())
						Logger.Warning("WebSocketで受信した " + e.Key + " の検証に失敗しています");

					using var stream = e.GetBodyStream();
					var report = (Report)ReportSerializer.Deserialize(stream);
					ProcessReport(report, false);
				};

				await DmdataSocket.ConnectAsync(new[] { TelegramCategory.Earthquake }, "KEVi " + Assembly.GetExecutingAssembly().GetName().Version);
			}
			catch (DmdataForbiddenException ex)
			{
				Logger.Error("WebSocketが利用できないためPULL型にフォールバックします\n" + ex);
				Status = DmdataStatus.UsingPullForForbidden;
				await PullXmlAsync(false);
			}
		}
		/// <summary>
		/// 課金状況を更新する
		/// <para>課金状態更新タイマーもセットし直します</para>
		/// </summary>
		private async Task UpdateBillingStatusAsync()
		{
			if (IgnoreBillingstatusCheck || string.IsNullOrWhiteSpace(ConfigService.Configuration.Dmdata.ApiKey))
			{
				BillingInfo = null;
				BillingInfoUpdated.Publish();
				return;
			}

			try
			{
				BillingInfo = await ApiClient.GetBillingInfoAsync();
				Logger.Info("課金情報を更新しました。");
			}
			catch (DmdataException ex)
			{
				Logger.Error("課金情報取得中に例外が発生しました。以降の課金情報の取得を中断します。\n" + ex);
				IgnoreBillingstatusCheck = true;
				BillingInfo = null;
			}
			BillingInfoUpdated.Publish();

			// サーバー負荷軽減のためランダムに5～10分の遅延を入れる
			UpdateBillingStatusTimer.Change(TimeSpan.FromSeconds(Random.Next(5 * 60, 10 * 60)), Timeout.InfiniteTimeSpan);
		}
	}

	public enum DmdataStatus
	{
		/// <summary>
		/// APIキーが空
		/// </summary>
		Stopping,
		/// <summary>
		/// APIキーが空ではないが必要なAPIの権限がなく利用できなかった
		/// </summary>
		StoppingForInvalidKey,
		/// <summary>
		/// 過去データ受信中
		/// </summary>
		Initalizing,
		/// <summary>
		/// APIリクエスト失敗
		/// </summary>
		Failed,
		/// <summary>
		/// WebSocketの権限がないためPULL型を利用している
		/// </summary>
		UsingPullForForbidden,
		/// <summary>
		/// ユーザーから明示的な切断要求があった場合や同時接続数オーバーためPULL型を利用している
		/// </summary>
		UsingPullForError,
		/// <summary>
		/// PULL型を利用している
		/// </summary>
		UsingPull,
		/// <summary>
		/// WebSocket再接続中
		/// </summary>
		ReconnectingWebSocket,
		/// <summary>
		/// WebSocket利用中
		/// </summary>
		UsingWebSocket,
	}
}
