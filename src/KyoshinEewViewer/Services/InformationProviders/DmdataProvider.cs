using DmdataSharp;
using DmdataSharp.ApiResponses.V2.Parameters;
using DmdataSharp.Authentication.OAuth;
using DmdataSharp.Exceptions;
using KyoshinEewViewer.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services.InformationProviders
{
	public class DmdataProvider : InformationProvider
	{
		private static DmdataProvider? _default;
		public static DmdataProvider Default => _default ??= new();

		public event Action? Authorized;

		private DmdataApiClientBuilder ClientBuilder { get; }
		private ILogger Logger { get; }
		public DmdataProvider()
		{
			ClientBuilder = DmdataApiClientBuilder.Default
				.Referrer(new Uri("https://www.ingen084.net/"))
				.UserAgent($"KEVi_{Assembly.GetExecutingAssembly().GetName().Version};@ingen084");
			Logger = LoggingService.CreateLogger(this);

			PullTimer = new Timer(async s => await PullFeedAsync());
		}

		/// <summary>
		/// チェックエンドポイント
		/// </summary>
		public const string INTROSPECT_ENDPOINT_URL = "https://manager.dmdata.jp/account/oauth2/v1/introspect";

		private DmdataV2ApiClient? ApiClient { get; set; }
		private DmdataV2Socket? Socket { get; set; }
		private string? CursorToken { get; set; }

		private string[] FetchTypes { get; set; } = Array.Empty<string>();
		private string? AccessToken { get; set; }
		private DateTime? AccessTokenExpires { get; set; }

		private Random Random { get; } = new Random();
		private Timer PullTimer { get; }

		private bool Enabled { get; set; }

		private static readonly string[] RequiredScope = new[]{
				"parameter.earthquake",
				"socket.start",
				"telegram.list",
				"telegram.data",
				"telegram.get.earthquake",
			};

		public async Task AuthorizeAsync()
		{
			(ConfigurationService.Current.Dmdata.RefreshToken, AccessToken, AccessTokenExpires) = await SimpleOAuthAuthenticator.AuthorizationAsync(
				ClientBuilder.HttpClient,
				ConfigurationService.Current.Dmdata.OAuthClientId,
				RequiredScope,
				"KyoshinEewViewer for ingen",
				url => UrlOpener.OpenUrl(url),
				"http://localhost:14191/",
				TimeSpan.FromMinutes(1));
			Authorized?.Invoke();
		}
		public async Task UnauthorizationAsync()
		{
			// TODO: 茶を濁す
			try
			{
				if (string.IsNullOrEmpty(ConfigurationService.Current.Dmdata.RefreshToken))
					return;
				var response = await ClientBuilder.HttpClient.PostAsync(OAuthCredential.REVOKE_ENDPOINT_URL, new FormUrlEncodedContent(new Dictionary<string, string?>()
				{
					{ "client_id", ConfigurationService.Current.Dmdata.OAuthClientId },
					{ "token", ConfigurationService.Current.Dmdata.RefreshToken },
				}));
				if (!response.IsSuccessStatusCode)
					Logger.LogWarning("リフレッシュトークンの無効化に失敗しました ");
			}
			catch (Exception ex)
			{
				Logger.LogError("リフレッシュトークンの無効化中に例外が発生しました {ex}", ex);
			}

			ConfigurationService.Current.Dmdata.RefreshToken = null;
			await StopAsync();
			OnStopped();
		}

		public class IntrospectResponse
		{
			[JsonPropertyName("active")]
			public bool Active { get; set; }
			[JsonPropertyName("scope")]
			public string? Scope { get; set; }
			[JsonPropertyName("error")]
			public string? Error { get; set; }
		}
		public async Task<bool> CheckScopesAsync()
		{
			// TODO: 茶を濁す
			if (string.IsNullOrEmpty(ConfigurationService.Current.Dmdata.RefreshToken))
				return false;
			var response = await ClientBuilder.HttpClient.PostAsync(INTROSPECT_ENDPOINT_URL, new FormUrlEncodedContent(new Dictionary<string, string?>()
				{
					{ "client_id", ConfigurationService.Current.Dmdata.OAuthClientId },
					{ "token", ConfigurationService.Current.Dmdata.RefreshToken },
				}));
			if (JsonSerializer.Deserialize<IntrospectResponse>(await response.Content.ReadAsStringAsync()) is IntrospectResponse r)
			{
				if (r.Error == "invalid_client")
				{
					ConfigurationService.Current.Dmdata.OAuthClientId = KyoshinEewViewerConfiguration.DmdataConfig.DefaultOAuthClientId;
					return false;
				}
				return r.Active && r.Scope?.Split(' ') is string[] scopes && scopes.SequenceEqual(RequiredScope);
			}
			return false;
		}

		public async override Task StartAsync(string[] fetchTitles, string[] fetchTypes)
		{
			FetchTypes = fetchTypes;

			if (ConfigurationService.Current.Dmdata.RefreshToken is not string refreshToken)
				throw new Exception("リフレッシュトークンが取得できません");

			if (Enabled)
				throw new Exception("すでに有効化されています");

			ClientBuilder.UseOAuthRefreshToken(
				ConfigurationService.Current.Dmdata.OAuthClientId,
				RequiredScope,
				refreshToken,
				AccessToken,
				AccessTokenExpires);

			ApiClient = ClientBuilder.BuildV2ApiClient();

			// リフレッシュトークン+スコープのチェック
			if (!await CheckScopesAsync())
			{
				await UnauthorizationAsync();
				return;
			}

			if (ConfigurationService.Current.Dmdata.UseWebSocket)
				try
				{
					await ConnectWebSocketAsync();
					return;
				}
				catch (Exception ex)
				{
					Logger.LogError("WebSocketの接続開始に失敗しました: {ex}", ex);
				}
			await StartPullAsync();
		}

		public Task<EarthquakeStationParameterResponse> GetEarthquakeStationsAsync()
			=> ApiClient?.GetEarthquakeStationParameterAsync() ?? throw new Exception("ApiClientが初期化されていません");

		private int FailCount { get; set; }
		private async Task ConnectWebSocketAsync()
		{
			if (ApiClient == null)
				throw new Exception("ApiClientが初期化されていません");
			if (Socket?.IsConnected ?? false)
				throw new Exception("すでにWebSocketに接続しています");

			Logger.LogInformation($"WebSocketに接続します");
			await SwitchInformationAsync();

			Socket = new DmdataV2Socket(ApiClient);
			Socket.Connected += (s, e) => Logger.LogInformation("WebSocket Connected id: {SocketId}", e?.SocketId);
			Socket.DataReceived += (s, e) =>
			{
				if (e is null || !e.Validate())
				{
					Logger.LogError("WebSocket電文 {Id} の検証に失敗しました", e?.Id);
					return;
				}
				if (e.XmlReport is null)
				{
					Logger.LogError("WebSocket電文 {Id} の XMLReport がありません", e.Id);
					return;
				}
				if (e.XmlReport.Head.Title is null)
				{
					Logger.LogError("WebSocket電文 {Id} の Title が取得できません", e.Id);
					return;
				}
				FailCount = 0;
				OnInformationArrived(new Information(
					e.Id,
					e.XmlReport.Head.Title,
					e.XmlReport.Control.DateTime,
					() => InformationCacheService.TryGetOrFetchTelegramAsync(e.Id, () => Task.FromResult(e.GetBodyStream())),
					() => InformationCacheService.DeleteTelegramCache(e.Id)));
			};
			Socket.Error += async (s, e) =>
			{
				if (e is null)
				{
					Logger.LogError("WebSocketエラーがnullです");
					return;
				}
				Logger.LogWarning("WebSocketエラー受信: {Error}({Code})", e.Error, e.Code);

				// エラーコードの上位2桁で判断する
				switch (e.Code / 100)
				{
					// リクエストに関連するエラー 手動での切断 契約終了の場合はPULL型に変更
					case 44:
					case 48:
						if (!e.Close)
							await Socket.DisconnectAsync();
						await StartPullAsync();
						return;
				}
				// それ以外の場合かつ切断された場合は再接続を試みる
				if (!e.Close)
					return;

				// 4回以上失敗していたらPULLに移行する
				FailCount++;
				if (FailCount >= 4)
				{
					await StartPullAsync();
					return;
				}

				await Socket.DisconnectAsync();
				await ConnectWebSocketAsync();
			};
			Socket.Disconnected += (s, e) => Logger.LogInformation($"WebSocketから切断されました");
			await Socket.ConnectAsync(new DmdataSharp.ApiParameters.V2.SocketStartRequestParameter(TelegramCategoryV1.Earthquake)
			{
				AppName = $"KEVi {Assembly.GetExecutingAssembly().GetName().Version}",
				Types = FetchTypes,
			});
		}
		private async Task StartPullAsync()
		{
			try
			{
				Logger.LogInformation("PULLを開始します");
				var interval = await SwitchInformationAsync();
				PullTimer.Change(TimeSpan.FromMilliseconds(interval * Math.Max(ConfigurationService.Current.Dmdata.PullMultiply, 1) * (1 + Random.NextDouble() * .2)), Timeout.InfiniteTimeSpan);
			}
			catch (Exception ex)
			{
				Logger.LogError("PULL開始中にエラーが発生しました {ex}", ex);
				await UnauthorizationAsync();
			}
		}

		private async Task<int> SwitchInformationAsync()
		{
			// ノリで1秒待機する
			await Task.Delay(1000);
			Logger.LogDebug("SwitchInformation");
			CursorToken = null;

			var (infos, interval) = await FetchListAsync();
			OnInformationSwitched(infos.Select(r => new Information(
				r.key,
				r.title,
				r.arrivalTime,
				() => InformationCacheService.TryGetOrFetchTelegramAsync(r.key, () => FetchContentAsync(r.key)),
				() => InformationCacheService.DeleteTelegramCache(r.key)
			)).ToArray());
			return interval;
		}
		private async Task PullFeedAsync()
		{
			try
			{
				if (Socket?.IsConnected ?? false)
				{
					Logger.LogWarning("WebSocket接続中にPullしようとしました");
					return;
				}
				var (infos, interval) = await FetchListAsync();

				foreach (var (key, title, arrivalTime) in infos.Reverse())
					OnInformationArrived(new Information(
						key,
						title,
						arrivalTime,
						() => InformationCacheService.TryGetOrFetchTelegramAsync(key, () => FetchContentAsync(key)),
						() => InformationCacheService.DeleteTelegramCache(key)));

				// レスポンスの時間*設定での倍率*1～1.2倍のランダム間隔でリクエストを行う
				PullTimer?.Change(TimeSpan.FromMilliseconds(interval * Math.Max(ConfigurationService.Current.Dmdata.PullMultiply, 1) * (1 + Random.NextDouble() * .2)), Timeout.InfiniteTimeSpan);
			}
			catch (Exception ex)
			{
				Logger.LogError("PULL受信中にエラーが発生しました {ex}", ex);
				OnStopped();
			}
		}

		private async Task<((string key, string title, DateTime arrivalTime)[], int nextPoolingInterval)> FetchListAsync()
		{
			if (ApiClient == null)
				throw new Exception("ApiClientが初期化されていません");

			var result = new List<(string key, string title, DateTime arrivalTime)>();

			Logger.LogDebug("get telegram list CursorToken: {CursorToken}", CursorToken);
			// 初回取得は震源震度に関する情報だけにしておく
			var resp = await ApiClient.GetTelegramListAsync(type: string.Join(",", FetchTypes), xmlReport: true, cursorToken: CursorToken, limit: 50);
			CursorToken = resp.NextPooling;

			// TODO: リトライ処理の実装
			if (resp.Status != "ok")
				throw new Exception($"dmdataからのリストの取得に失敗しました status: {resp.Status}, errorMessage: {resp.Error?.Message}");

			Logger.LogInformation("dmdata items count: {count}", resp.Items.Length);
			foreach (var item in resp.Items)
			{
				// 解析すべき情報だけ取ってくる
				if (item.Format != "xml")
					continue;

				var xmlReport = item.XmlReport ?? throw new Exception("XMLReportが取得できません: " + item.Id);
				result.Add((
					item.Id,
					xmlReport.Head.Title ?? throw new Exception("titleが取得できません: " + item.Id),
					xmlReport.Head.ReportDateTime));
			}

			Logger.LogDebug("get telegram list nextpooling: {interval}", resp.NextPoolingInterval);
			result.Reverse();
			return (result.ToArray(), resp.NextPoolingInterval);
		}
		private async Task<Stream> FetchContentAsync(string key)
		{
			var count = 0;
			while (true)
			{
				count++;
				try
				{
					Logger.LogInformation("dmdataから取得しています: {key}", key);
					return await (ApiClient?.GetTelegramStreamAsync(key) ?? throw new Exception("ApiClientが初期化されていません"));
				}
				catch (DmdataRateLimitExceededException ex)
				{
					Logger.LogWarning("レートリミットに引っかかっています try{count} ({RetryAfter})", count, ex.RetryAfter);
					if (count > 10)
						throw;
					await Task.Delay(200);
				}
				catch(Exception ex)
				{
					Logger.LogError("電文取得中にエラーが発生しました {ex}", ex);
					OnStopped();
					throw;
				}
			}
		}

		public async override Task StopAsync()
		{
			PullTimer.Change(Timeout.Infinite, Timeout.Infinite);
			if (Socket?.IsConnected ?? false)
				await Socket.DisconnectAsync();
			Socket = null;
			ApiClient = null;
			Enabled = false;
		}
	}
}
