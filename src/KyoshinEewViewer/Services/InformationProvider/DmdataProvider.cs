using DmdataSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services.InformationProvider
{
	public class DmdataProvider
	{
		public List<Earthquake> Earthquakes { get; } = new List<Earthquake>();
		private EarthquakeUpdated EarthquakeUpdated { get; }

		public DmdataSharp.ApiResponses.V1.BillingResponse BillingInfo { get; private set; }
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
		private DmdataV2ApiClient ApiClient { get; }
		private DmdataV2Socket DmdataSocket { get; set; }
		private ConfigurationService ConfigService { get; }

		/// <summary>
		/// telegram.listで使用するAPI
		/// </summary>
		private string NextPooling { get; set; }

		private XmlSerializer ReportSerializer { get; } = new XmlSerializer(typeof(Report));
		private readonly string[] ParseTitles = { "震度速報", "震源に関する情報", "震源・震度に関する情報" };

		public DmdataProvider(ConfigurationService configService)
		{
			ConfigService = configService;
			Logger = logger;
			ApiClient = DmdataApiClientBuilder.Default.UseApiKey(ConfigurationService.Default.Dmdata.ApiKey).BuildV2ApiClient();

			EarthquakeUpdated = eventAggregator.GetEvent<EarthquakeUpdated>();
			BillingInfoUpdated = eventAggregator.GetEvent<DmdataBillingInfoUpdated>();
			StatusUpdated = eventAggregator.GetEvent<DmdataStatusUpdated>();

			ConfigService.Configuration.Dmdata.PropertyChanged += async (s, e) =>
			{
				switch (e.PropertyName)
				{
					case nameof(ConfigService.Configuration.Dmdata.ApiKey):
						Logger.Info("dmdataのAPIキーが更新されました");
						if (ApiClient.Authenticator is ApiKeyAuthenticator apiKeyAuthenticator)
							apiKeyAuthenticator.ApiKey = ConfigService.Configuration.Dmdata.ApiKey;

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
				Logger.Debug("get telegram list: " + NextPooling);
				// 初回取得は震源震度に関する情報だけにしておく
				var resp = await ApiClient.GetTelegramListAsync(type: firstSync ? "VXSE53" : "VXSE5", xmlReport: true, cursorToken: NextPooling, limit: 5);
				NextPooling = resp.NextPooling;

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
					if (item.Format != "xml" || !ParseTitles.Contains(item.XmlReport.Control.Title))
						continue;

					Logger.Info("dmdataから取得しています: " + item.Id);
					using var rstr = await ApiClient.GetTelegramStreamAsync(item.Id);
					var report = (Report)ReportSerializer.Deserialize(rstr);

					ProcessReport(report, firstSync);
				}

				if (firstSync)
					EarthquakeUpdated.Publish(null);

				Logger.Debug("get telegram list nextpooling: " + resp.NextPoolingInterval);
				// レスポンスの時間*設定での倍率*1～1.2倍のランダム間隔でリクエストを行う
				PullingTimer.Change(TimeSpan.FromMilliseconds(resp.NextPoolingInterval * Math.Max(ConfigService.Configuration.Dmdata.PullMultiply, 1) * (1 + Random.NextDouble() * .2)), Timeout.InfiniteTimeSpan);
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
					IsHypocenterOnly = false,
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
						// とりあえず最大震度は更新しておく
						var infoItem = report.Head.Headline.Informations.First().Items.First();
						eq.Intensity = infoItem.Kind.Name.Replace("震度", "").ToJmaIntensity();

						// すでに他の情報が入ってきている場合更新を行わない
						if (!eq.IsSokuhou)
							break;
						// eq.IsHypocenterOnly = false;
						eq.IsSokuhou = true;
						eq.OccurrenceTime = report.Head.TargetDateTime;
						eq.IsReportTime = true;

						eq.Place = infoItem.Areas.Area.First().Name;
						break;
					}
				case "震源に関する情報":
					{
						if (eq.IsSokuhou)
						{
							eq.IsHypocenterOnly = true;
							eq.IsSokuhou = false;
						}
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
						eq.IsHypocenterOnly = false;
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
				DmdataSocket = new DmdataV2Socket(ApiClient);
				DmdataSocket.Connected += (s, e) =>
				{
					Logger.Info("WebSocketに接続完了しました " + e.Type);
					Status = DmdataStatus.UsingWebSocket;
				};
				DmdataSocket.Disconnected += async (s, e) =>
				{
					Logger.Info("WebSocketから切断されました");
				};
				DmdataSocket.Error += async (s, e) =>
				{
					switch (e.Code)
					{
						// サーバー再起動･契約解約の場合は再接続を試みる
						case 4503:
						case 4807:
							await TryConnectWebSocketAsync();
							return;
					}
					// それ以外の場合はエラー扱いとしてPULL型へ
					Status = DmdataStatus.UsingPullForError;
					await DmdataSocket.DisconnectAsync();
				};
				DmdataSocket.DataReceived += async (s, e) =>
				{
					Logger.Info("WebSocket受信: " + e.Id);
					// 処理できない電文を処理しない
					if (e.XmlReport == null || !ParseTitles.Contains(e.XmlReport.Control?.Title))
						return;

					// 検証が正しくない場合はパケットが破損しているのでIdで取得し直す
					if (!e.Validate())
					{
						try
						{
							Logger.Warning("WebSocketで受信した " + e.Id + " の検証に失敗しています");
							using var rstr = await ApiClient.GetTelegramStreamAsync(e.Id);
							ProcessReport((Report)ReportSerializer.Deserialize(rstr), false);
						}
						catch (Exception ex)
						{
							Logger.Error("WebSocketで受信した " + e.Id + " の再取得に失敗しました" + ex);
						}
						return;
					}

					try
					{
						using var stream = e.GetBodyStream();
						ProcessReport((Report)ReportSerializer.Deserialize(stream), false);
					}
					catch (Exception ex)
					{
						Logger.Error("WebSocketで受信した " + e.Id + " の処理に失敗しました" + ex);
					}
				};

				await DmdataSocket.ConnectAsync(new DmdataSharp.ApiParameters.V2.SocketStartRequestParameter(TelegramCategoryV1.Earthquake)
				{
					AppName = "KEVi " + Assembly.GetExecutingAssembly().GetName().Version,
					Types = new[] {
						"VXSE51",
						"VXSE52",
						"VXSE53",
					},
				});
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
			catch (Exception ex)
			{
				Logger.Error("課金情報取得中に例外が発生しました。以降の課金情報の取得を中断します。\n" + ex);
				IgnoreBillingstatusCheck = true;
				BillingInfo = null;
			}
			BillingInfoUpdated.Publish();

			// サーバー負荷軽減のためランダムに10～60分の遅延を入れる
			UpdateBillingStatusTimer.Change(TimeSpan.FromSeconds(Random.Next(10 * 60, 60 * 60)), Timeout.InfiniteTimeSpan);
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
