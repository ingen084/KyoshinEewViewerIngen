using DmdataSharp;
using DmdataSharp.Authentication;
using DmdataSharp.Exceptions;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using System;
using System.IO;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services.InformationProvider
{
	public class DmdataProvider
	{
		public event Action<InformationHeader, Stream?>? NewDataArrived;

		private DmdataStatus status = 0;
		public DmdataStatus Status
		{
			get => status;
			set
			{
				status = value;
				StatusUpdated?.Invoke();
			}
		}
		public bool Available => Status switch
		{
			DmdataStatus.Stopping => false,
			DmdataStatus.Failed => false,
			DmdataStatus.StoppingForInvalidKey => false,
			DmdataStatus.StoppingForNeedPermission => false,
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
		private DmdataV2Socket? DmdataSocket { get; set; }
		private ILogger Logger { get; }

		public event Action? StatusUpdated;

		/// <summary>
		/// telegram.listで使用するAPI
		/// </summary>
		private string? NextPooling { get; set; }

		public DmdataProvider()
		{
			Logger = LoggingService.CreateLogger(this);
			ApiClient = DmdataApiClientBuilder.Default.UseApiKey(ConfigurationService.Default.Dmdata.ApiKey).BuildV2ApiClient();

			ConfigurationService.Default.Dmdata.WhenAnyValue(x => x.ApiKey).Throttle(TimeSpan.FromSeconds(2)).Subscribe(x =>
			{
				Logger.LogInformation("dmdataのAPIキーが更新されました");
				if (ApiClient.Authenticator is ApiKeyAuthenticator apiKeyAuthenticator)
					apiKeyAuthenticator.ApiKey = x;
				InitalizeAsync().ConfigureAwait(false);
			});

			ConfigurationService.Default.Dmdata.WhenAnyValue(x => x.UseWebSocket).Throttle(TimeSpan.FromSeconds(2)).Subscribe(x =>
			{
				Logger.LogInformation("WebSocketの接続がトグルされました");
				InitalizeAsync().ConfigureAwait(false);
			});

			PullingTimer = new Timer(async s => await PullXmlAsync(false), null, Timeout.Infinite, Timeout.Infinite);
		}

		public async Task InitalizeAsync()
		{
			// セットしていない場合停止中に
			if (string.IsNullOrWhiteSpace(ConfigurationService.Default.Dmdata.ApiKey))
			{
				Logger.LogInformation("APIキーが存在しないためdmdataは利用しません");
				// WebSocketに接続していたら切断
				if (DmdataSocket != null)
					await DmdataSocket.DisconnectAsync();
				Status = DmdataStatus.Stopping;
				return;
			}
			// 初回データのDL+WebSocketに接続するまでは初期化状態
			Status = DmdataStatus.Initalizing;
			await PullXmlAsync(true);
			// APIキー変更でWebSocketに再接続を行う
			if (DmdataSocket != null)
				await DmdataSocket.DisconnectAsync();
			await TryConnectWebSocketAsync();
		}

		public async Task<Stream> GetTelegramStreamAsync(string key)
		{
			Logger.LogInformation("dmdataから取得しています: " + key);
			return await ApiClient.GetTelegramStreamAsync(key);
		}

		private async Task PullXmlAsync(bool firstSync)
		{
			// PULLモードでない場合無視
			if (Status != DmdataStatus.UsingPull && Status != DmdataStatus.UsingPullForForbidden && Status != DmdataStatus.UsingPullForError && Status != DmdataStatus.Initalizing)
				return;

			try
			{
				Logger.LogTrace("get telegram list: " + NextPooling);
				// 初回取得は震源震度に関する情報だけにしておく
				var resp = await ApiClient.GetTelegramListAsync(type: "VXSE51,VXSE52,VXSE53", xmlReport: true, cursorToken: NextPooling, limit: 20);
				NextPooling = resp.NextPooling;

				// TODO: リトライ処理の実装
				if (resp.Status != "ok")
				{
					Status = DmdataStatus.Failed;
					Logger.LogInformation($"dmdataからのリストの取得に失敗しました status: {resp.Status}, errorMessage: {resp.Error?.Message}");
					return;
				}
				Logger.LogInformation($"dmdata items: " + resp.Items.Length);
				foreach (var item in resp.Items)
				{
					// 解析すべき情報だけ取ってくる
					if (item.Format != "xml")
						continue;

					var header = new InformationHeader(InformationSource.Dmdata, item.Id, item.XmlReport?.Head.Title, DateTime.Now, null);
					NewDataArrived?.Invoke(header, null);
				}

				Logger.LogTrace("get telegram list nextpooling: " + resp.NextPoolingInterval);
				// レスポンスの時間*設定での倍率*1～1.2倍のランダム間隔でリクエストを行う
				PullingTimer.Change(TimeSpan.FromMilliseconds(resp.NextPoolingInterval * Math.Max(ConfigurationService.Default.Dmdata.PullMultiply, 1) * (1 + Random.NextDouble() * .2)), Timeout.InfiniteTimeSpan);
			}
			catch (DmdataUnauthorizedException ex)
			{
				Logger.LogError("APIキーが不正です\n" + ex);
				Status = DmdataStatus.StoppingForInvalidKey;
			}
			catch (DmdataForbiddenException ex)
			{
				Logger.LogError("必須APIを利用する権限がありません\n" + ex);
				Status = DmdataStatus.StoppingForNeedPermission;
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
				Logger.LogInformation("APIキーが存在しないためdmdataは利用しません");
				Status = DmdataStatus.Stopping;
				return;
			}
			// WebSocketを利用しない場合
			if (!ConfigurationService.Default.Dmdata.UseWebSocket)
			{
				Logger.LogInformation("WebSocketを利用しない設定になっています");
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
					Logger.LogWarning("すでにWebSocketに接続中でした");
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
					Logger.LogInformation("WebSocketに接続完了しました " + e?.Type);
					Status = DmdataStatus.UsingWebSocket;
				};
				DmdataSocket.Disconnected += (s, e) =>
				{
					Logger.LogInformation("WebSocketから切断されました");
				};
				DmdataSocket.Error += async (s, e) =>
				{
					switch (e?.Code)
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
					Logger.LogInformation("WebSocket受信: " + e?.Id);
					// 処理できない電文を処理しない
					if (e?.XmlReport == null)
						return;

					var header = new InformationHeader(InformationSource.Dmdata, e.Id, e.XmlReport.Head.Title, DateTime.Now, null);

					// 検証が正しくない場合はパケットが破損しているので取得し直してもらう
					if (!e.Validate())
					{
						Logger.LogWarning("WebSocketで受信した " + e.Id + " の検証に失敗しています");
						NewDataArrived?.Invoke(header, null);
						return;
					}

					NewDataArrived?.Invoke(header, e.GetBodyStream());
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
			catch (DmdataException ex)
			{
				Logger.LogError("WebSocketが利用できないためPULL型にフォールバックします\n" + ex);
				Status = DmdataStatus.UsingPullForForbidden;
				await PullXmlAsync(false);
			}
		}
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
