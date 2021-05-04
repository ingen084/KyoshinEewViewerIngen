namespace KyoshinEewViewer.Services.InformationProvider
{
	public class DmdataProvider
	{
		// TODO: 一旦コメントアウト
		/*
		public List<Earthquake> Earthquakes { get; } = new List<Earthquake>();
		private EarthquakeUpdated EarthquakeUpdated { get; }

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

		public Timer PullingTimer { get; }

		private Random Random { get; } = new Random();
		private DmdataV2ApiClient ApiClient { get; }
		private DmdataV2Socket DmdataSocket { get; set; }

		/// <summary>
		/// telegram.listで使用するAPI
		/// </summary>
		private string NextPooling { get; set; }

		public DmdataProvider()
		{
			ApiClient = DmdataApiClientBuilder.Default.UseApiKey(ConfigurationService.Default.Dmdata.ApiKey).BuildV2ApiClient();

			ConfigurationService.Default.Dmdata.WhenAnyValue(x => x.ApiKey).Throttle(TimeSpan.FromSeconds(2)).Subscribe(x =>
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
			});

			PullingTimer = new Timer(async s => await PullXmlAsync(false), null, Timeout.Infinite, Timeout.Infinite);
		}

		public async Task InitalizeAsync()
		{
			// セットしていない場合停止中に
			if (string.IsNullOrWhiteSpace(ConfigurationService.Default.Dmdata.ApiKey))
			{
				Trace.TraceInformation("APIキーが存在しないためdmdataは利用しません");
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
				Trace.WriteLine("get telegram list: " + NextPooling);
				// 初回取得は震源震度に関する情報だけにしておく
				var resp = await ApiClient.GetTelegramListAsync(type: firstSync ? "VXSE53" : "VXSE5", xmlReport: true, cursorToken: NextPooling, limit: 5);
				NextPooling = resp.NextPooling;

				// TODO: リトライ処理の実装
				if (resp.Status != "ok")
				{
					Status = DmdataStatus.Failed;
					Trace.TraceInformation($"dmdataからのリストの取得に失敗しました status: {resp.Status}, errorMessage: {resp.Error?.Message}");
					return;
				}
				Trace.TraceInformation($"dmdata items: " + resp.Items.Length);
				foreach (var item in resp.Items)
				{
					// 解析すべき情報だけ取ってくる
					if (item.Format != "xml" || !ParseTitles.Contains(item.XmlReport.Control.Title))
						continue;
					
					Trace.TraceInformation("dmdataから取得しています: " + item.Id);
					using var rstr = await ApiClient.GetTelegramStreamAsync(item.Id);
					var report = (Report)ReportSerializer.Deserialize(rstr);

					ProcessReport(report, firstSync);
				}

				if (firstSync)
					EarthquakeUpdated.Publish(null);

				Trace.WriteLine("get telegram list nextpooling: " + resp.NextPoolingInterval);
				// レスポンスの時間*設定での倍率*1～1.2倍のランダム間隔でリクエストを行う
				PullingTimer.Change(TimeSpan.FromMilliseconds(resp.NextPoolingInterval * Math.Max(ConfigService.Configuration.Dmdata.PullMultiply, 1) * (1 + Random.NextDouble() * .2)), Timeout.InfiniteTimeSpan);
			}
			catch (DmdataForbiddenException ex)
			{
				Logger.Error("必須APIを利用する権限がないもしくはAPIキーが不正です\n" + ex);
				Status = DmdataStatus.StoppingForInvalidKey;
			}
		}

		/// <summary>
		/// 可能であればWebSocketへの接続を行い、ステータスの更新を行う
		/// </summary>
		/// <returns></returns>
		private async Task TryConnectWebSocketAsync()
		{
			// セットしていない場合停止中に
			if (string.IsNullOrWhiteSpace(ConfigurationService.Default.Dmdata.ApiKey))
			{
				Trace.TraceInformation("APIキーが存在しないためdmdataは利用しません");
				Status = DmdataStatus.Stopping;
				return;
			}
			// WebSocketを利用しない場合
			if (!ConfigurationService.Default.Dmdata.UseWebSocket)
			{
				Trace.TraceInformation("WebSocketを利用しない設定になっています");
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
					Trace.TraceWarning("すでにWebSocketに接続中でした");
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
					Trace.TraceInformation("WebSocketに接続完了しました " + e.Type);
					Status = DmdataStatus.UsingWebSocket;
				};
				DmdataSocket.Disconnected += (s, e) =>
				{
					Trace.TraceInformation("WebSocketから切断されました");
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
					Trace.TraceInformation("WebSocket受信: " + e.Id);
					// 処理できない電文を処理しない
					if (e.XmlReport == null || !ParseTitles.Contains(e.XmlReport.Control?.Title))
						return;

					// 検証が正しくない場合はパケットが破損しているのでIdで取得し直す
					if (!e.Validate())
					{
						try
						{
							Trace.TraceWarning("WebSocketで受信した " + e.Id + " の検証に失敗しています");
							using var rstr = await ApiClient.GetTelegramStreamAsync(e.Id);
							ProcessReport((Report)ReportSerializer.Deserialize(rstr), false);
						}
						catch (Exception ex)
						{
							Trace.TraceError("WebSocketで受信した " + e.Id + " の再取得に失敗しました" + ex);
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
						Trace.TraceError("WebSocketで受信した " + e.Id + " の処理に失敗しました" + ex);
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
				Trace.TraceError("WebSocketが利用できないためPULL型にフォールバックします\n" + ex);
				Status = DmdataStatus.UsingPullForForbidden;
				await PullXmlAsync(false);
			}
		}
		*/
	}

	public enum DmdataStatus
	{
		/// <summary>
		/// APIキーが空
		/// </summary>
		Stopping,
		/// <summary>
		/// APIキーが不正のため利用できなかった
		/// </summary>
		StoppingForInvalidKey,
		/// <summary>
		/// 必要なAPIの権限がなく利用できなかった
		/// </summary>
		StoppingForNeedPermission,
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
