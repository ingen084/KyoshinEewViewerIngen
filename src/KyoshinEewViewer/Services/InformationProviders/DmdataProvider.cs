using DmdataSharp;
using DmdataSharp.ApiResponses.V2.Parameters;
using DmdataSharp.Authentication.OAuth;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services.InformationProviders
{
	public class DmdataProvider : InformationProvider
	{
		private static DmdataProvider? _default;
		public static DmdataProvider Default => _default ??= new();

		private DmdataApiClientBuilder ClientBuilder { get; }
		private ILogger Logger { get; }
		public DmdataProvider()
		{
			ClientBuilder = DmdataApiClientBuilder.Default
				.Referrer(new Uri("https://www.ingen084.net/"))
				.UserAgent($"KEVi_{Assembly.GetExecutingAssembly().GetName().Version};@ingen084");
			Logger = LoggingService.CreateLogger(this);

			PullTimer = new Timer(async s =>
			{
				try
				{
					var (infos, interval) = await FetchListAsync();

					foreach (var (key, title, arrivalTime) in infos.Reverse())
						OnInformationArrived(new Information(key, title, arrivalTime, ()
							=> InformationCacheService.Default.TryGetOrFetchContentAsync(key, title, arrivalTime, () => FetchContentAsync(key))));

					// レスポンスの時間*設定での倍率*1～1.2倍のランダム間隔でリクエストを行う
					PullTimer?.Change(TimeSpan.FromMilliseconds(interval * Math.Max(ConfigurationService.Default.Dmdata.PullMultiply, 1) * (1 + Random.NextDouble() * .2)), Timeout.InfiniteTimeSpan);
				}
				catch (Exception ex)
				{
					Logger.LogError("PULL受信中にエラーが発生しました " + ex);
					State = null;
				}
			});
		}

		private DmdataV2ApiClient? ApiClient { get; set; }
		private string? CursorToken { get; set; }

		private string[] FetchTypes { get; set; } = Array.Empty<string>();
		private string? AccessToken { get; set; }
		private DateTime? AccessTokenExpires { get; set; }

		private Random Random { get; } = new Random();
		private Timer PullTimer { get; }


		private string[] GetScopes()
		{
			var scopes = new List<string>()
			{
				"parameter.earthquake",
				"socket.start",
				"telegram.list",
				"telegram.data",
				"telegram.get.earthquake",
			};
			return scopes.ToArray();
		}

		public async Task AuthorizeAsync()
			=> (ConfigurationService.Default.Dmdata.RefleshToken, AccessToken, AccessTokenExpires) = await SimpleOAuthAuthenticator.AuthorizationAsync(
				ClientBuilder.HttpClient,
				ConfigurationService.Default.Dmdata.OAuthClientId,
				GetScopes(),
				"KyoshinEewViewer for ingen",
				url => UrlOpener.OpenUrl(url),
				"http://localhost:14191/",
				TimeSpan.FromMinutes(10));
		public async Task UnauthorizationAsync()
		{
			// TODO: 茶を濁す
			try
			{
				if (string.IsNullOrEmpty(ConfigurationService.Default.Dmdata.RefleshToken))
					return;
#pragma warning disable CS8620 // 参照型の NULL 値の許容の違いにより、パラメーターに引数を使用できません。
				var response = await ClientBuilder.HttpClient.PostAsync(OAuthCredential.REVOKE_ENDPOINT_URL, new FormUrlEncodedContent(new Dictionary<string, string?>()
				{
					{ "client_id", ConfigurationService.Default.Dmdata.OAuthClientId },
					{ "token", ConfigurationService.Default.Dmdata.RefleshToken },
				}));
#pragma warning restore CS8620 // 参照型の NULL 値の許容の違いにより、パラメーターに引数を使用できません。

				if (!response.IsSuccessStatusCode)
					Logger.LogWarning("リフレッシュトークンの無効化に失敗しました ");
			}
			catch (Exception ex)
			{
				Logger.LogError("リフレッシュトークンの無効化中に例外が発生しました " + ex);
			}

			ConfigurationService.Default.Dmdata.RefleshToken = null;
		}

		public async override Task<Information[]> StartAndPullInformationsAsync(string[] fetchTitles, string[] fetchTypes)
		{
			FetchTypes = fetchTypes;

			if (ConfigurationService.Default.Dmdata.RefleshToken is not string refleshToken)
				throw new Exception("リフレッシュトークンが取得できません");

			ClientBuilder.UseOAuthRefleshToken(
				ConfigurationService.Default.Dmdata.OAuthClientId,
				GetScopes(),
				refleshToken,
				AccessToken,
				AccessTokenExpires);

			ApiClient = ClientBuilder.BuildV2ApiClient();

			var (result, interval) = await FetchListAsync();

			PullTimer.Change(TimeSpan.FromMilliseconds(interval * Math.Max(ConfigurationService.Default.Dmdata.PullMultiply, 1) * (1 + Random.NextDouble() * .2)), Timeout.InfiniteTimeSpan);
			return result.Select(r => new Information(r.key, r.title, r.arrivalTime, ()
				=> InformationCacheService.Default.TryGetOrFetchContentAsync(r.key, r.title, r.arrivalTime, () => FetchContentAsync(r.key)))).ToArray();
		}

		public Task<EarthquakeStationParameterResponse> GetEarthquakeStationsAsync()
			=> ApiClient?.GetEarthquakeStationParameterAsync() ?? throw new Exception("ApiClientが初期化されていません");

		private async Task<((string key, string title, DateTime arrivalTime)[], int nextPoolingInterval)> FetchListAsync()
		{
			if (ApiClient == null)
				throw new Exception("ApiClientが初期化されていません");

			var result = new List<(string key, string title, DateTime arrivalTime)>();

			Logger.LogDebug("get telegram list " + CursorToken);
			// 初回取得は震源震度に関する情報だけにしておく
			var resp = await ApiClient.GetTelegramListAsync(type: string.Join(",", FetchTypes), xmlReport: true, cursorToken: CursorToken);
			CursorToken = resp.NextPooling;

			// TODO: リトライ処理の実装
			if (resp.Status != "ok")
				throw new Exception($"dmdataからのリストの取得に失敗しました status: {resp.Status}, errorMessage: {resp.Error?.Message}");

			Logger.LogInformation($"dmdata items: " + resp.Items.Length);
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

			Logger.LogDebug("get telegram list nextpooling: " + resp.NextPoolingInterval);
			result.Reverse();
			return (result.ToArray(), resp.NextPoolingInterval);
		}

		private Task<Stream> FetchContentAsync(string key)
		{
			Logger.LogInformation("dmdataから取得しています: " + key);
			return ApiClient?.GetTelegramStreamAsync(key) ?? throw new Exception("ApiClientが初期化されていません");
		}

		public override Task StopAsync() => throw new NotImplementedException();
	}
}
