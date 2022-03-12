using DmdataSharp;
using DmdataSharp.ApiResponses.V2.Parameters;
using DmdataSharp.Authentication.OAuth;
using DmdataSharp.Exceptions;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services.TelegramPublishers.Dmdata;

public class DmdataTelegramPublisher : TelegramPublisher
{
	// FIXME: 苦しい 治す
	public static DmdataTelegramPublisher? Instance { get; private set; }

	// 認可を求めるスコープ
	private static readonly string[] RequiredScope = new[]{
		"contract.list",
		"parameter.earthquake",
		"socket.start",
		"socket.close",
		"telegram.list",
		"telegram.data",
		"telegram.get.earthquake",
	};

	// スコープからカテゴリへのマップ
	private static readonly Dictionary<string, InformationCategory[]> CategoryMap = new()
	{
		{ "telegram.earthquake", new[] { InformationCategory.Earthquake } },
	};

	// カテゴリからタイプ郡へのマップ
	private static readonly Dictionary<InformationCategory, string[]> TypeMap = new()
	{
		{
			InformationCategory.Earthquake,
			new[]
			{
				"VXSE51",
				"VXSE52",
				"VXSE53",
				"VXSE61",
			}
		},
	};

	private DmdataApiClientBuilder ClientBuilder { get; } = DmdataApiClientBuilder.Default
			.Referrer(new Uri("https://www.ingen084.net/"))
			.UserAgent($"KEVi_{Assembly.GetExecutingAssembly().GetName().Version};@ingen084");
	private OAuthRefreshTokenCredential? Credential { get; set; }
	private DmdataV2ApiClient? ApiClient { get; set; }
	private DmdataV2Socket? Socket { get; set; }
	private string? CursorToken { get; set; }

	/// <summary>
	/// 購読中のカテゴリ
	/// </summary>
	private List<InformationCategory> SubscribingCategories { get; } = new();

	private ILogger Logger { get; } = LoggingService.CreateLogger<DmdataTelegramPublisher>();

	private Random Random { get; } = new Random();
	private Timer PullTimer { get; }
	private Timer SettingsApplyTimer { get; }

	public DmdataTelegramPublisher()
	{
		PullTimer = new Timer(async s => await PullFeedAsync());
		SettingsApplyTimer = new Timer(async _ =>
		{
			if (ApiClient == null)
				return;
			await StartInternalAsync();
		});
		Instance = this;
	}

	public async Task<EarthquakeStationParameterResponse?> GetEarthquakeStationsAsync()
	{
		if (ApiClient is null)
			return null;
		return await ApiClient.GetEarthquakeStationParameterAsync();
	}

	public override Task InitalizeAsync()
	{
		// 設定ファイルから読み出し
		if (ConfigurationService.Current.Dmdata.RefreshToken == null)
			return Task.CompletedTask;

		Credential = new(
			ClientBuilder.HttpClient,
			RequiredScope,
			ConfigurationService.Current.Dmdata.OAuthClientId,
			ConfigurationService.Current.Dmdata.RefreshToken);
		ClientBuilder.UseOAuth(Credential);
		ApiClient = ClientBuilder.BuildV2ApiClient();

		ConfigurationService.Current.Dmdata.WhenAnyValue(x => x.UseWebSocket, x => x.ReceiveTraining)
			.Skip(1) // 起動時に1回イベントが発生してしまうのでスキップする
			.Subscribe(_ => SettingsApplyTimer.Change(1000, Timeout.Infinite));

		return Task.CompletedTask;
	}

	public async Task AuthorizeAsync(CancellationToken cancellationToken)
	{
		var credentials = await SimpleOAuthAuthenticator.AuthorizationAsync(
			ClientBuilder.HttpClient,
			ConfigurationService.Current.Dmdata.OAuthClientId,
			RequiredScope,
			"KyoshinEewViewer for ingen",
			url => UrlOpener.OpenUrl(url),
			token: cancellationToken);
		// 認可でリフレッシュトークンを更新
		Credential = credentials;
		ConfigurationService.Current.Dmdata.RefreshToken = Credential.RefreshToken;
		ClientBuilder.UseOAuth(Credential);
		ApiClient = ClientBuilder.BuildV2ApiClient();
		// 更新通知を流しプロバイダを切り替えてもらう
		OnInformationCategoryUpdated();
	}
	public Task UnauthorizeAsync()
		=> FailAsync();

	public async override Task<InformationCategory[]> GetSupportedCategoriesAsync()
	{
		if (Credential == null)
			return Array.Empty<InformationCategory>();
		if (ApiClient == null)
			throw new InvalidOperationException("ApiClientが初期化されていません");

		try
		{
			var contracts = await ApiClient.GetContractListAsync();

			// 必須スコープが存在することを確認する
			if (contracts.Status != "ok")
			{
				Logger.LogError(
					"contract.list に失敗しました。status:{status} code:{code} message:{message}",
					contracts.Status,
					contracts.Error?.Code,
					contracts.Error?.Message
				);
				await FailAsync();
				return Array.Empty<InformationCategory>();
			}

			return contracts.Items.Where(c => c.IsValid && CategoryMap.ContainsKey(c.Classification))
				.Select(s => s.Classification)
				.SelectMany(s => CategoryMap[s]).ToArray();
		}
		catch (DmdataException ex)
		{
			Logger.LogError(
				"contract.list に失敗しました。{ex}", ex);
			await FailAsync();
			return Array.Empty<InformationCategory>();
		}
	}


	private int FailCount { get; set; }
	private int? LastConnectedWebSocketId { get; set; }
	private bool WebSocketDisconnecting { get; set; }
	private async Task StartWebSocketAsync()
	{
		if (WebSocketDisconnecting)
			return;
		if (ApiClient == null)
			throw new InvalidOperationException("ApiClientが初期化されていません");
		if (Socket?.IsConnected ?? false)
			throw new DmdataException("すでにWebSocketに接続しています");

		Logger.LogInformation($"WebSocketに接続します");
		await SwitchInformationAsync("WS");

		Socket = new DmdataV2Socket(ApiClient);
		Socket.Connected += (s, e) =>
		{
			Logger.LogInformation("WebSocket Connected id: {SocketId}", e?.SocketId);
			LastConnectedWebSocketId = e?.SocketId;
		};
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

			if (!TypeMap.Any(c => c.Value.Contains(e.Head.Type)))
				return;

			OnTelegramArrived(
				TypeMap.First(c => c.Value.Contains(e.Head.Type)).Key,
				new Telegram(
					e.Id,
					e.XmlReport.Head.Title,
					e.XmlReport.Control.DateTime,
					() => InformationCacheService.TryGetOrFetchTelegramAsync(e.Id, () => Task.FromResult(e.GetBodyStream())),
					() => InformationCacheService.DeleteTelegramCache(e.Id)
				)
			);
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
					WebSocketDisconnecting = true;
					if (!e.Close)
						await Socket.DisconnectAsync();
					OnFailed(SubscribingCategories.ToArray(), true);
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
				OnFailed(SubscribingCategories.ToArray(), true);
				await StartPullAsync();
				return;
			}

			await Socket.DisconnectAsync();
		};
		Socket.Disconnected += async (s, e) =>
		{
			Logger.LogInformation($"WebSocketから切断されました");
			if (!WebSocketDisconnecting)
				await StartWebSocketAsync();
		};
		WebSocketDisconnecting = false;
		try
		{
			if (LastConnectedWebSocketId is int lastId)
				try
				{
					await ApiClient.CloseSocketAsync(lastId);
				}
				catch (DmdataApiErrorException) { }

			await Socket.ConnectAsync(new DmdataSharp.ApiParameters.V2.SocketStartRequestParameter(TelegramCategoryV1.Earthquake)
			{
				AppName = $"KEVi {Assembly.GetExecutingAssembly().GetName().Version}",
				Types = SubscribingCategories.Where(c => TypeMap.ContainsKey(c)).SelectMany(c => TypeMap[c]).ToArray(),
				Test = ConfigurationService.Current.Dmdata.ReceiveTraining ? "including" : "no",
			});
		}
		catch (Exception ex)
		{
			Logger.LogError("WebSocket接続中に例外が発生したためPULL型に切り替えます: {ex}", ex);
			await StartPullAsync();
		}
	}
	private async Task StartPullAsync()
	{
		try
		{
			Logger.LogInformation("PULLを開始します");
			CursorToken = null;
			var interval = await SwitchInformationAsync("PULL");
			PullTimer.Change(TimeSpan.FromMilliseconds(interval * Math.Max(ConfigurationService.Current.Dmdata.PullMultiply, 1) * (1 + Random.NextDouble() * .2)), Timeout.InfiniteTimeSpan);
		}
		catch (Exception ex)
		{
			Logger.LogError("PULL開始中にエラーが発生しました {ex}", ex);
			await FailAsync();
		}
	}

	private async Task<int> SwitchInformationAsync(string add)
	{
		CursorToken = null;

		var (infos, interval) = await FetchListAsync();
		var telegramGroup = infos.Where(t => TypeMap.Any(m => m.Value.Contains(t.type))).GroupBy(t => TypeMap.First(m => m.Value.Contains(t.type)).Key);
		foreach (var c in SubscribingCategories)
			OnHistoryTelegramArrived(
				$"DM-D.S.S({add})",
				c,
				telegramGroup.FirstOrDefault(g => g.Key == c)?.Select(r => new Telegram(
					r.key,
					r.title,
					r.arrivalTime,
					() => InformationCacheService.TryGetOrFetchTelegramAsync(r.key, () => FetchContentAsync(r.key)),
					() => InformationCacheService.DeleteTelegramCache(r.key)
				))?.ToArray() ?? Array.Empty<Telegram>());
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

			foreach (var (key, title, type, arrivalTime) in infos.Reverse())
			{
				if (!TypeMap.Any(c => c.Value.Contains(type)))
					continue;
				OnTelegramArrived(
					TypeMap.First(c => c.Value.Contains(type)).Key,
					new Telegram(
						key,
						title,
						arrivalTime,
						() => InformationCacheService.TryGetOrFetchTelegramAsync(key, () => FetchContentAsync(key)),
						() => InformationCacheService.DeleteTelegramCache(key)
					)
				);
			}

			// レスポンスの時間*設定での倍率*1～1.2倍のランダム間隔でリクエストを行う
			PullTimer?.Change(TimeSpan.FromMilliseconds(interval * Math.Max(ConfigurationService.Current.Dmdata.PullMultiply, 1) * (1 + Random.NextDouble() * .2)), Timeout.InfiniteTimeSpan);
		}
		catch (Exception ex)
		{
			Logger.LogError("PULL受信中にエラーが発生しました {ex}", ex);
			await FailAsync();
		}
	}
	private async Task<((string key, string title, string type, DateTime arrivalTime)[], int nextPoolingInterval)> FetchListAsync()
	{
		if (ApiClient == null)
			throw new DmdataException("ApiClientが初期化されていません");

		var result = new List<(string key, string title, string type, DateTime arrivalTime)>();

		Logger.LogDebug("get telegram list CursorToken: {CursorToken}", CursorToken);
		// 初回取得は震源震度に関する情報だけにしておく
		var resp = await ApiClient.GetTelegramListAsync(
			type: string.Join(",", SubscribingCategories.Where(c => TypeMap.ContainsKey(c)).SelectMany(c => TypeMap[c])),
			xmlReport: true,
			test: ConfigurationService.Current.Dmdata.ReceiveTraining ? "including" : "no",
			cursorToken: CursorToken,
			limit: 50
		);
		CursorToken = resp.NextPooling;

		// TODO: リトライ処理の実装
		if (resp.Status != "ok")
			throw new DmdataException($"dmdataからのリストの取得に失敗しました status: {resp.Status}, errorMessage: {resp.Error?.Message}");

		Logger.LogDebug("dmdata items count: {count}", resp.Items.Length);
		foreach (var item in resp.Items)
		{
			// 解析すべき情報だけ取ってくる
			if (item.Format != "xml")
				continue;

			var xmlReport = item.XmlReport ?? throw new Exception("XMLReportが取得できません: " + item.Id);
			result.Add((
				item.Id,
				xmlReport.Head.Title ?? throw new Exception("titleが取得できません: " + item.Id),
				item.Head.Type,
				xmlReport.Head.ReportDateTime));
		}

		Logger.LogDebug("get telegram list nextpooling: {interval}", resp.NextPoolingInterval);
		if (result.Any())
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
			catch (Exception ex)
			{
				Logger.LogError("電文取得中にエラーが発生しました {ex}", ex);
				await FailAsync();
				throw;
			}
		}
	}

	public async override void Start(InformationCategory[] categories)
	{
		// 新規追加するもののみ抽出
		var added = categories.Where(c => !SubscribingCategories.Contains(c));
		if (!added.Any())
			return;
		/// 追加があった場合、接続し直す
		SubscribingCategories.AddRange(added);
		await StartInternalAsync();
	}

	/// <summary>
	/// WebSocketの接続状況を設定に同期する
	/// </summary>
	/// <returns></returns>
	public async Task StartInternalAsync()
	{
		if (ApiClient == null)
			throw new DmdataException("ApiClient が初期化されていません");

		// 停止
		PullTimer.Change(Timeout.Infinite, Timeout.Infinite);
		if (Socket?.IsConnected ?? false)
		{
			WebSocketDisconnecting = true;
			await Socket.DisconnectAsync();
		}
		Socket = null;

		// 開始
		if (ConfigurationService.Current.Dmdata.UseWebSocket)
		{
			WebSocketDisconnecting = false;
			await StartWebSocketAsync();
		}
		else
			await StartPullAsync();
	}

	public async override void Stop(InformationCategory[] categories)
	{
		SubscribingCategories.RemoveAll(c => categories.Contains(c));
		if (!SubscribingCategories.Any())
			await StopInternalAsync();
	}

	private async Task StopInternalAsync()
	{
		WebSocketDisconnecting = true;
		PullTimer.Change(Timeout.Infinite, Timeout.Infinite);
		if (Socket?.IsConnected ?? false)
			await Socket.DisconnectAsync();
		Socket = null;
		ApiClient = null;
	}

	/// <summary>
	/// 速やかに認可情報を失効させ、処理を終了する
	/// </summary>
	private async Task FailAsync()
	{
		await StopInternalAsync();
		try
		{
			Credential?.RevokeRefreshTokenAsync();
		}
		catch (Exception ex)
		{
			Logger.LogWarning("失効時のリフレッシュトークンの無効化に失敗しました: {ex}", ex);
		}
		Credential = null;
		ConfigurationService.Current.Dmdata.RefreshToken = null;

		OnFailed(SubscribingCategories.ToArray(), false);
		SubscribingCategories.Clear();
	}
}
