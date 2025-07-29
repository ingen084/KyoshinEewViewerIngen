using DmdataSharp;
using DmdataSharp.ApiParameters.V2;
using DmdataSharp.ApiResponses.V2.Parameters;
using DmdataSharp.Authentication.OAuth;
using DmdataSharp.Exceptions;
using DmdataSharp.Redundancy;
using DmdataSharp.WebSocketMessages.V2;
using DynamicData;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services.TelegramPublishers.Dmdata;

public class DmdataRedundantTelegramPublisher : TelegramPublisher, IDisposable
{
	/// <summary>
	/// 接続状態が変更されたときに発生するイベント
	/// </summary>
	public event EventHandler? ConnectionStatusChanged;
	// 認可を求めるスコープ
	private static readonly string[] RequiredScope = [
		"contract.list",
		"parameter.earthquake",
		"socket.start",
		"socket.close",
		"telegram.list",
		"telegram.data",
		"telegram.get.earthquake",
		"eew.get.forecast",
		"eew.get.warning",
	];
	// 追加で認可を求めるスコープ
	private static readonly string[] AdditionalScope = [
		"parameter.tsunami",
		"telegram.get.weather",
	];

	// スコープからカテゴリへのマップ
	private static readonly Dictionary<string, InformationCategory[]> CategoryMap = new()
	{
		{
			"telegram.earthquake",
			[
				InformationCategory.Earthquake,
				InformationCategory.Tsunami,
			]
		},
		{
			"telegram.weather",
			[
				InformationCategory.Typhoon,
			]
		},
		{ "eew.forecast", new[] { InformationCategory.EewForecast } },
		{ "eew.warning", new[] { InformationCategory.EewWarning } },
	};

	// カテゴリからカテゴリへのマップ
	private static readonly Dictionary<InformationCategory, TelegramCategoryV1> TelegramCategoryMap = new()
	{
		{ InformationCategory.Earthquake, TelegramCategoryV1.Earthquake },
		{ InformationCategory.Tsunami, TelegramCategoryV1.Earthquake },
		{ InformationCategory.Typhoon, TelegramCategoryV1.Weather },
		{ InformationCategory.EewForecast, TelegramCategoryV1.EewForecast },
		{ InformationCategory.EewWarning, TelegramCategoryV1.EewWarning },
	};

	// カテゴリからタイプ郡へのマップ
	private static readonly Dictionary<InformationCategory, string[]> TypeMap = new()
	{
		{
			InformationCategory.Earthquake,
			[
				"VXSE51",
				"VXSE52",
				"VXSE53",
				"VXSE61",
				"VXSE62",
			]
		},
		{
			InformationCategory.EewForecast,
			[
				"VXSE42",
				"VXSE45",
			]
		},
		{ InformationCategory.EewWarning, [ "VXSE43" ] },
		{
			InformationCategory.Tsunami,
			[
				"VTSE41",
				"VTSE51",
				"VTSE52",
			]
		},
		{
			InformationCategory.Typhoon,
			[
				"VPTW60",
				"VPTW61",
				"VPTW62",
				"VPTW63",
				"VPTW64",
				"VPTW65",
			]
		}
	};

	private DmdataApiClientBuilder ClientBuilder { get; } = DmdataApiClientBuilder.Default
			.Referrer(new Uri("https://www.ingen084.net/"))
			.UserAgent($"KEVi_{Utils.Version};@ingen084");
	private OAuthCredential? Credential { get; set; }
	private DmdataV2ApiClient? ApiClient { get; set; }
	private RedundantDmdataSocketController? RedundantController { get; set; }
	private string? CursorToken { get; set; }

	/// <summary>
	/// 購読中のカテゴリ
	/// </summary>
	public ObservableCollection<InformationCategory> SubscribingCategories { get; } = [];

	/// <summary>
	/// 冗長性状態
	/// </summary>
	private RedundancyStatus _redundancyStatus = RedundancyStatus.Disconnected;
	public RedundancyStatus RedundancyStatus
	{
		get => _redundancyStatus;
		private set => this.RaiseAndSetIfChanged(ref _redundancyStatus, value);
	}

	/// <summary>
	/// アクティブ接続数
	/// </summary>
	private int _activeConnectionCount = 0;
	public int ActiveConnectionCount
	{
		get => _activeConnectionCount;
		private set => this.RaiseAndSetIfChanged(ref _activeConnectionCount, value);
	}

	/// <summary>
	/// 接続中のエンドポイント
	/// </summary>
	private string[] _connectedEndpoints = [];
	public string[] ConnectedEndpoints
	{
		get => _connectedEndpoints;
		private set => this.RaiseAndSetIfChanged(ref _connectedEndpoints, value);
	}

	/// <summary>
	/// 受信した総メッセージ数
	/// </summary>
	private long _totalMessagesReceived = 0;
	public long TotalMessagesReceived
	{
		get => _totalMessagesReceived;
		private set => this.RaiseAndSetIfChanged(ref _totalMessagesReceived, value);
	}

	/// <summary>
	/// 最後にメッセージを受信した時刻
	/// </summary>
	public DateTime? LastMessageTime => RedundantController?.LastMessageTime;

	private ILogger Logger { get; }
	private KyoshinEewViewerConfiguration Config { get; }
	private InformationCacheService CacheService { get; }

	private Random Random { get; } = new Random();
	private Timer PullTimer { get; }
	private int ReconnectBackoffTime { get; set; } = 10;
	private Timer WebSocketReconnectTimer { get; }
	private Timer TemporaryFailureRecoveryTimer { get; }
	private IDisposable? _configSubscription;

	public DmdataRedundantTelegramPublisher(ILogManager logManager, KyoshinEewViewerConfiguration config, InformationCacheService cacheService)
	{
		SplatRegistrations.RegisterLazySingleton<DmdataRedundantTelegramPublisher>();

		Logger = logManager.GetLogger<DmdataRedundantTelegramPublisher>();
		Config = config;
		CacheService = cacheService;

		PullTimer = new(async s => await PullFeedAsync());
		WebSocketReconnectTimer = new(async s =>
		{
			var shouldReconnect = ApiClient != null &&
				SubscribingCategories.Any() &&
				Config.Dmdata.UseWebSocket &&
				RedundancyStatus == RedundancyStatus.Disconnected &&
				CurrentState != ConnectionState.Connecting &&
				CurrentState != ConnectionState.Disconnecting &&
				CurrentState != ConnectionState.TemporaryFailure;

			if (shouldReconnect)
			{
				Logger.LogInfo("WebSocketへの再接続を試みます");
				await StartInternalAsync();
				ReconnectBackoffTime = Math.Min(600, ReconnectBackoffTime * 2);
			}
			WebSocketReconnectTimer?.Change(TimeSpan.FromSeconds(ReconnectBackoffTime), Timeout.InfiniteTimeSpan);
		}, null, TimeSpan.FromSeconds(10), Timeout.InfiniteTimeSpan);

		TemporaryFailureRecoveryTimer = new(async s =>
		{
			if (CurrentState == ConnectionState.TemporaryFailure && ApiClient != null)
			{
				Logger.LogInfo($"一時的な障害から復旧を試みます (試行回数: {TemporaryFailureCount})");
				try
				{
					await StartInternalAsync();
				}
				catch (Exception ex)
				{
					Logger.LogError(ex, "一時的な障害からの復旧試行中に例外が発生しました");
				}
			}
		}, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
	}

	public async Task<EarthquakeStationParameterResponse?> GetEarthquakeStationsAsync()
	{
		if (ApiClient is null)
			return null;
		return await ApiClient.GetEarthquakeStationParameterAsync();
	}

	public async Task<TsunamiStationParameterResponse?> GetTsunamiStationsAsync()
	{
		if (ApiClient is null)
			return null;
		return await ApiClient.GetTsunamiStationParameterAsync();
	}

	public override Task InitializeAsync()
	{
		if (Config.Dmdata.RefreshToken != null)
		{
			Credential = new OAuthRefreshTokenCredential(
				ClientBuilder.HttpClient,
				RequiredScope,
				Config.Dmdata.OAuthClientId,
				Config.Dmdata.RefreshToken);
			ClientBuilder.UseOAuth(Credential);
			ApiClient = ClientBuilder.BuildV2ApiClient();
		}
		else if (!string.IsNullOrWhiteSpace(Config.Dmdata.OAuthClientSecret))
		{
			Credential = new OAuthClientCredential(
				ClientBuilder.HttpClient,
				RequiredScope,
				Config.Dmdata.OAuthClientId,
				Config.Dmdata.OAuthClientSecret);
			ClientBuilder.UseOAuth(Credential);
			ApiClient = ClientBuilder.BuildV2ApiClient();
		}
		else
			return Task.CompletedTask;

		_configSubscription = Config.Dmdata.WhenAnyValue(x => x.UseWebSocket, x => x.ReceiveTraining, x => x.UseRedundancy)
			.Skip(1)
			.Throttle(TimeSpan.FromSeconds(1))
			.Subscribe(async _ =>
			{
				if (ApiClient == null)
					return;
				await StartInternalAsync();
			});

		return Task.CompletedTask;
	}

	public async Task AuthorizeAsync(CancellationToken cancellationToken)
	{
		var credentials = await SimpleOAuthAuthenticator.AuthorizationAsync(
			ClientBuilder.HttpClient,
			Config.Dmdata.OAuthClientId,
			RequiredScope.Concat(AdditionalScope).ToArray(),
			"KyoshinEewViewer for ingen",
			UrlOpener.OpenUrl,
			token: cancellationToken);
		Credential = credentials;
		Config.Dmdata.RefreshToken = credentials.RefreshToken;
		ClientBuilder.UseOAuth(Credential);
		ApiClient = ClientBuilder.BuildV2ApiClient();
		OnInformationCategoryUpdated();
	}

	public async Task UnauthorizeAsync()
	{
		Logger.LogInfo("認可を解除します");
		await FailAsync();
	}

	public async override Task<InformationCategory[]> GetSupportedCategoriesAsync()
	{
		if (Credential == null)
			return [];
		if (ApiClient == null)
			throw new InvalidOperationException("ApiClientが初期化されていません");

		try
		{
			var contracts = await ApiClient.GetContractListAsync();

			if (contracts.Status != "ok")
			{
				Logger.LogError($"contract.list に失敗しました。status:{contracts.Status} code:{contracts.Error?.Code} message:{contracts.Error?.Message}");
				// 認証エラーの場合のみ完全失効
				if (IsAuthenticationErrorCode(contracts.Error?.Code.ToString()))
					await FailAsync();
				else
					await TemporaryFailAsync("contract.listエラー");
				return [];
			}

			var categories = contracts.Items.Where(c => c.IsValid && CategoryMap.ContainsKey(c.Classification))
				.Select(s => s.Classification)
				.SelectMany(s => CategoryMap[s]).ToArray();

			// WebSocketが無効な場合、EEW関連のカテゴリを除外
			if (!Config.Dmdata.UseWebSocket)
			{
				categories = categories.Where(c => c != InformationCategory.EewForecast && c != InformationCategory.EewWarning).ToArray();
			}

			return categories;
		}
		catch (DmdataException ex)
		{
			Logger.LogError(ex, "contract.list に失敗しました");
			// 認証エラーの場合のみ完全失効
			if (IsAuthenticationError(ex))
				await FailAsync();
			else
				await TemporaryFailAsync("contract.listでDmdataException");
			return [];
		}
	}

	/// <summary>
	/// 接続状態を表す列挙型
	/// </summary>
	private enum ConnectionState
	{
		Disconnected,
		Connecting,
		WebSocketConnected,
		PullConnected,
		Disconnecting,
		Failed,
		/// <summary>
		/// 一時的な障害状態（認証情報は保持）
		/// </summary>
		TemporaryFailure
	}

	private ConnectionState CurrentState { get; set; } = ConnectionState.Disconnected;
	private int FailCount { get; set; }
	private int TemporaryFailureCount { get; set; }
	private DateTime? LastTemporaryFailureTime { get; set; }

	/// <summary>
	/// WebSocket接続を開始する
	/// </summary>
	private async Task StartWebSocketAsync()
	{
		if (CurrentState == ConnectionState.Connecting ||
			CurrentState == ConnectionState.Disconnecting ||
			CurrentState == ConnectionState.WebSocketConnected)
			return;

		if (ApiClient == null)
			throw new InvalidOperationException("ApiClientが初期化されていません");

		Logger.LogInfo("WebSocketに接続します");
		CurrentState = ConnectionState.Connecting;

		try
		{
			await SwitchInformationAsync(true);

			// RedundantControllerを初期化
			RedundantController?.Dispose();
			RedundantController = new RedundantDmdataSocketController(ApiClient);
			ConfigureRedundantControllerEvents();

			var classifications = SubscribingCategories.Select(c => TelegramCategoryMap[c]).Distinct().ToArray();
			if (classifications.Length <= 0)
			{
				Logger.LogInfo("取得対象が存在しないため接続しません");
				OnFailed(SubscribingCategories.ToArray(), false);

				CurrentState = ConnectionState.Disconnected;
				return;
			}

			var parameter = new SocketStartRequestParameter(classifications)
			{
				AppName = $"KEVi v{Utils.Version}",
				Types = SubscribingCategories.Where(TypeMap.ContainsKey).SelectMany(c => TypeMap[c]).ToArray(),
				Test = Config.Dmdata.ReceiveTraining ? "including" : "no",
			};

			// 冗長性設定に基づいてエンドポイントを選択
			var endpoints = Config.Dmdata.UseRedundancy
				? RedundantSocketOptions.DefaultEndpoints
				: [DmdataV2SocketEndpoints.Global];

			await RedundantController.ConnectAsync(parameter, endpoints);
			UpdateConnectionStatus();

			CurrentState = ConnectionState.WebSocketConnected;
		}
		catch (Exception ex)
		{
			// 認証エラーの場合のみ完全失効
			if (IsAuthenticationError(ex))
			{
				Logger.LogError(ex, "WebSocket接続中に認証エラーが発生しました");

				OnFailed(SubscribingCategories.ToArray(), false);
				CurrentState = ConnectionState.Failed;
				return;
			}

			OnFailed(SubscribingCategories.ToArray(), true);
			CurrentState = ConnectionState.Disconnected;

			Logger.LogError(ex, "WebSocket接続中に例外が発生したためPULL型に切り替えます");
			
			await StartPullAsync();
		}
	}

	/// <summary>
	/// RedundantControllerのイベントハンドラを設定する
	/// </summary>
	private void ConfigureRedundantControllerEvents()
	{
		if (RedundantController == null)
			return;

		RedundantController.DataReceived += async (s, e) =>
		{
			try
			{
				await ProcessWebSocketDataAsync(e);
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, "WebSocketデータ処理中に例外が発生しました");
			}
		};

		// 生データイベントで統計更新
		RedundantController.RawDataReceived += (s, e) =>
		{
			// UI表示用の統計情報も更新
			TotalMessagesReceived = RedundantController.TotalMessagesReceived;
		};

		RedundantController.AllConnectionsLost += async (s, e) =>
		{
			try
			{
				Logger.LogWarning("すべての接続が失われました");
				FailCount++;
				UpdateConnectionStatus();
				ConnectionStatusChanged?.Invoke(this, EventArgs.Empty);
				if (FailCount >= 3)
				{
					Logger.LogInfo("接続失敗回数が上限に達したためPULL型に切り替えます");
					OnFailed(SubscribingCategories.ToArray(), true);
					await StartPullAsync();
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, "全接続失効イベント処理中に例外が発生しました");
			}
		};

		RedundantController.RedundancyRestored += (s, e) =>
		{
			Logger.LogInfo($"冗長性が復旧しました エンドポイント:{e.RestoredEndpoint} アクティブ接続数:{e.TotalActiveConnections}");
			FailCount = 0;
			ReconnectBackoffTime = 10;
			UpdateConnectionStatus();
			ConnectionStatusChanged?.Invoke(this, EventArgs.Empty);
		};

		RedundantController.ConnectionError += (s, e) =>
		{
			Logger.LogWarning($"接続エラーが発生しました エンドポイント:{e.EndpointName} エラー:{e.ErrorMessage?.ToString() ?? e.Exception?.Message}");
			UpdateConnectionStatus();
			ConnectionStatusChanged?.Invoke(this, EventArgs.Empty);
		};

		// 接続開始時に統計をリセット
		RedundantController.ConnectionEstablished += (s, e) =>
		{
			try
			{
				UpdateConnectionStatus();
				ConnectionStatusChanged?.Invoke(this, EventArgs.Empty);
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, "接続確立イベント処理中に例外が発生しました");
			}
		};
	}

	/// <summary>
	/// 接続状態を更新する
	/// </summary>
	private void UpdateConnectionStatus()
	{
		if (RedundantController != null)
		{
			RedundancyStatus = RedundantController.Status;
			ActiveConnectionCount = RedundantController.ActiveConnectionCount;
			ConnectedEndpoints = RedundantController.ConnectedEndpoints ?? [];
			TotalMessagesReceived = RedundantController.TotalMessagesReceived;
		}
		else
		{
			RedundancyStatus = RedundancyStatus.Disconnected;
			ActiveConnectionCount = 0;
			ConnectedEndpoints = [];
			TotalMessagesReceived = 0;
		}
	}

	/// <summary>
	/// WebSocketから受信したデータを処理する
	/// </summary>
	private async Task ProcessWebSocketDataAsync(DataWebSocketMessage? e)
	{
		if (e is null)
		{
			Logger.LogError("WebSocketデータがnullです");
			return;
		}

#if DEBUG
		var sb = new System.Text.StringBuilder();
		foreach (var p in e.Passing)
			sb.Append($" {p.Name}:{p.Time:ss.fff}");
		Logger.LogDebug($"{e.Head.Type}{sb}");
#endif

		if (e.XmlReport is null)
		{
			Logger.LogError($"WebSocket電文 {e.Id} の XMLReport がありません");
			return;
		}
		if (e.XmlReport.Head.Title is null)
		{
			Logger.LogError($"WebSocket電文 {e.Id} の Title が取得できません");
			return;
		}
		FailCount = 0;

		if (!TypeMap.Any(c => c.Value.Contains(e.Head.Type)))
			return;
		var category = TypeMap.First(c => c.Value.Contains(e.Head.Type)).Key;
		if (!SubscribingCategories.Contains(category))
			return;

		if (category == InformationCategory.EewForecast || category == InformationCategory.EewWarning)
		{
			OnTelegramArrived(
				category,
				new DmdataEewTelegram(e)
			);
			return;
		}

		await using var stream = e.GetBodyStream();
		var mstream = new MemoryStream();
		await stream.CopyToAsync(mstream);
		OnTelegramArrived(
			category,
			new DmdataTelegram(
				e.Id,
				e.XmlReport.Control.Title,
				e.Head.Type,
				e.XmlReport.Control.DateTime,
				this,
				mstream.ToArray()
			)
		);

		_ = Task.Run(async () =>
		{
			try
			{
				mstream.Seek(0, SeekOrigin.Begin);
				await CacheService.CacheTelegramAsync(e.Id, () => mstream);
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, "電文のキャッシュに失敗しました");
			}
			finally
			{
				mstream.Dispose();
			}
		}).ConfigureAwait(false);
	}

	/// <summary>
	/// PULL接続を開始する
	/// </summary>
	private async Task StartPullAsync()
	{
		if (CurrentState == ConnectionState.Connecting ||
			CurrentState == ConnectionState.Disconnecting ||
			CurrentState == ConnectionState.PullConnected)
			return;

		Logger.LogInfo("PULLを開始します");
		CurrentState = ConnectionState.Connecting;

		try
		{
			if (!SubscribingCategories.Any(c => c != InformationCategory.EewForecast && c != InformationCategory.EewWarning))
			{
				Logger.LogInfo("PULLできるカテゴリが存在しなかったため何もしません");
				CurrentState = ConnectionState.Disconnected;
				return;
			}

			var interval = await SwitchInformationAsync(false);
			PullTimer.Change(TimeSpan.FromMilliseconds(interval * Math.Max(Config.Dmdata.PullMultiply, 1) * (1 + Random.NextDouble() * .2)), Timeout.InfiniteTimeSpan);
			CurrentState = ConnectionState.PullConnected;
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "PULL開始中にエラーが発生しました");
			
			// 認証エラーの場合のみ完全失効
			if (IsAuthenticationError(ex))
			{
				CurrentState = ConnectionState.Failed;
				await FailAsync();
			}
			else
			{
				await TemporaryFailAsync("PULL開始エラー");
			}
		}
	}

	/// <summary>
	/// 情報ソースを切り替える
	/// </summary>
	private async Task<int> SwitchInformationAsync(bool isWebSocket)
	{
		CursorToken = null;
		ReceivedTelegrams.Clear();

		var interval = 1000;

		foreach (var c in SubscribingCategories)
		{
			if (c == InformationCategory.EewForecast || c == InformationCategory.EewWarning)
			{
				if (isWebSocket)
					OnHistoryTelegramArrived(
						"DM-D.S.S(WS)",
						c,
						[]);
				continue;
			}

			(var infos, interval) = await FetchListAsync(c, false);
			OnHistoryTelegramArrived(
				$"DM-D.S.S({(isWebSocket ? "WS" : "PULL")})",
				c,
				infos.Select(r => new DmdataTelegram(
					r.key,
					r.title,
					r.type,
					r.arrivalTime,
					this
				)).ToArray());
			await Task.Delay(interval);
		}
		return interval;
	}

	/// <summary>
	/// PULLでフィードを取得する
	/// </summary>
	private async Task PullFeedAsync()
	{
		if (CurrentState != ConnectionState.PullConnected)
		{
			Logger.LogWarning($"PULL接続中でない状態({CurrentState})でPullしようとしました");
			return;
		}

		try
		{
			if (RedundantController?.IsConnected ?? false)
			{
				Logger.LogWarning("WebSocket接続中にPullしようとしました");
				return;
			}

			var (infos, interval) = await FetchListAsync(null, true);

			foreach (var (key, title, type, arrivalTime) in infos.Reverse())
			{
				if (!TypeMap.Any(c => c.Value.Contains(type)))
					continue;
				var category = TypeMap.First(c => c.Value.Contains(type)).Key;
				if (!SubscribingCategories.Contains(category))
					continue;

				OnTelegramArrived(
					category,
					new DmdataTelegram(
						key,
						title,
						type,
						arrivalTime,
						this
					)
				);
			}

			PullTimer?.Change(TimeSpan.FromMilliseconds(interval * Math.Max(Config.Dmdata.PullMultiply, 1)), Timeout.InfiniteTimeSpan);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "PULL受信中にエラーが発生しました");
			
			// 認証エラーの場合のみ完全失効
			if (IsAuthenticationError(ex))
				await FailAsync();
			else
				await TemporaryFailAsync("PULL受信エラー");
		}
	}

	private List<string> ReceivedTelegrams { get; } = [];
	private async Task<((string key, string title, string type, DateTime arrivalTime)[], int nextPoolingInterval)> FetchListAsync(InformationCategory? filterCategory, bool useCursorToken)
	{
		if (ApiClient == null)
			throw new DmdataException("ApiClientが初期化されていません");

		var result = new List<(string key, string title, string type, DateTime arrivalTime)>();

		Logger.LogDebug($"get telegram list CursorToken: {CursorToken}");

		string? type = null;
		if (filterCategory is { } ca)
		{
			if (ca == InformationCategory.Typhoon)
				type = "VPTW";
			else
				type = string.Join(",", TypeMap[ca]);
		}
		var resp = await ApiClient.GetTelegramListAsync(
			type: type,
			xmlReport: true,
			test: Config.Dmdata.ReceiveTraining ? "including" : "no",
			cursorToken: useCursorToken ? CursorToken : null,
			limit: 50
		);

		if (resp.Status != "ok")
			throw new DmdataException($"dmdataからのリストの取得に失敗しました status: {resp.Status}, errorMessage: {resp.Error?.Message}");

		Logger.LogDebug($"dmdata items count: {resp.Items.Length}");
		foreach (var item in resp.Items)
		{
			if (item.Format != "xml" || ReceivedTelegrams.Contains(item.Id))
				continue;

			result.Add((
				item.Id,
				item.XmlReport!.Control.Title!,
				item.Head.Type,
				item.XmlReport!.Control.DateTime));

			if (!useCursorToken)
				ReceivedTelegrams.Add(item.Id);
		}
		if (useCursorToken)
		{
			CursorToken = resp.NextPooling;
			ReceivedTelegrams.Clear();
		}

		Logger.LogDebug($"get telegram list nextpooling: {resp.NextPoolingInterval}");
		if (result.Count != 0)
			result.Reverse();
		return (result.ToArray(), resp.NextPoolingInterval);
	}

	internal async Task<Stream> FetchContentAsync(string key)
	{
		var count = 0;
		while (true)
		{
			count++;
			try
			{
				Logger.LogInfo($"dmdataから取得しています: {key}");
				return await (ApiClient?.GetTelegramStreamAsync(key) ?? throw new Exception("ApiClientが初期化されていません"));
			}
			catch (DmdataRateLimitExceededException ex)
			{
				Logger.LogWarning($"レートリミットに引っかかっています try{count} ({ex.RetryAfter})");
				if (count > 10)
					throw;
				await Task.Delay(200);
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, "電文取得中にエラーが発生しました");
				
				// 認証エラーの場合のみ完全失効
				if (IsAuthenticationError(ex))
					await FailAsync();
				else
					await TemporaryFailAsync("電文取得エラー");
				throw;
			}
		}
	}

	public async override void Start(InformationCategory[] categories)
	{
		var added = categories.Where(c => !SubscribingCategories.Contains(c));
		if (!added.Any())
			return;
		SubscribingCategories.AddRange(added.ToArray());
		await StartInternalAsync();
	}

	public async Task StartInternalAsync()
	{
		if (ApiClient == null)
			throw new DmdataException("ApiClient が初期化されていません");

		if (CurrentState == ConnectionState.WebSocketConnected ||
			CurrentState == ConnectionState.PullConnected)
		{
			CurrentState = ConnectionState.Disconnecting;
		}

		PullTimer.Change(Timeout.Infinite, Timeout.Infinite);
		TemporaryFailureRecoveryTimer.Change(Timeout.Infinite, Timeout.Infinite);

		if (RedundantController != null)
		{
			await RedundantController.DisconnectAsync();
			UpdateConnectionStatus();
		}

		CurrentState = ConnectionState.Disconnected;

		if (Config.Dmdata.UseWebSocket)
			await StartWebSocketAsync();
		else
			await StartPullAsync();

		// 成功した場合は一時的な障害カウントをリセットし、復旧を通知
		if (CurrentState == ConnectionState.WebSocketConnected || CurrentState == ConnectionState.PullConnected)
		{
			if (TemporaryFailureCount > 0)
			{
				Logger.LogInfo($"一時的な障害から復旧しました (試行回数: {TemporaryFailureCount})");
				TemporaryFailureCount = 0;
				LastTemporaryFailureTime = null;
				
				// 復旧を通知
				Logger.LogInfo("優先度の高いプロバイダとして復旧を通知します");
				OnInformationCategoryUpdated();
			}
		}
	}

	public async override void Stop(InformationCategory[] categories)
	{
		SubscribingCategories.RemoveMany(SubscribingCategories.Where(c => categories.Contains(c)).ToArray());
		if (!SubscribingCategories.Any())
			await StopInternalAsync();
	}

	private async Task StopInternalAsync()
	{
		CurrentState = ConnectionState.Disconnecting;

		PullTimer.Change(Timeout.Infinite, Timeout.Infinite);

		if (RedundantController != null)
		{
			await RedundantController.DisconnectAsync();
			RedundantController.Dispose();
			RedundantController = null;
			UpdateConnectionStatus();
		}
		ApiClient = null;

		CurrentState = ConnectionState.Disconnected;
	}

	/// <summary>
	/// 認証エラーかどうかを判定する
	/// </summary>
	private static bool IsAuthenticationError(Exception ex)
	{
		return ex switch
		{
			DmdataAuthenticationException => true,
			DmdataException dmdataEx when dmdataEx.Message.Contains("401") => true,
			_ => false
		};
	}

	/// <summary>
	/// エラーコードが認証エラーかどうかを判定する
	/// </summary>
	private static bool IsAuthenticationErrorCode(string? errorCode)
	{
		return errorCode switch
		{
			"401" or "401-1" or "401-2" or "401-3" => true,
			_ => false
		};
	}

	/// <summary>
	/// 一時的な障害状態に移行し、復旧を試みる
	/// </summary>
	private async Task TemporaryFailAsync(string reason)
	{
		CurrentState = ConnectionState.TemporaryFailure;
		TemporaryFailureCount++;
		LastTemporaryFailureTime = DateTime.Now;

		Logger.LogWarning($"一時的な障害が発生しました: {reason} (試行回数: {TemporaryFailureCount})");

		// 一時的な障害を通知し、フォールバックを有効化
		// isRestorable=falseにしてフォールバックを実行させる
		OnFailed(SubscribingCategories.ToArray(), false);

		// 現在の接続を停止
		PullTimer.Change(Timeout.Infinite, Timeout.Infinite);
		if (RedundantController != null)
		{
			await RedundantController.DisconnectAsync();
			UpdateConnectionStatus();
		}

		// 指数バックオフで再試行間隔を計算 (10秒から最大300秒)
		var retryInterval = Math.Min(300, 10 * Math.Pow(2, Math.Min(TemporaryFailureCount - 1, 5)));
		Logger.LogDebug($"{retryInterval}秒後に再試行します");

		// 再試行タイマーをセット
		TemporaryFailureRecoveryTimer.Change(TimeSpan.FromSeconds(retryInterval), Timeout.InfiniteTimeSpan);
	}

	/// <summary>
	/// 速やかに認可情報を失効させ、処理を終了する
	/// </summary>
	private async Task FailAsync()
	{
		CurrentState = ConnectionState.Failed;

		await StopInternalAsync();
		try
		{
			Credential?.RevokeRefreshTokenAsync();
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "失効時のリフレッシュトークンの無効化に失敗しました");
		}
		Credential = null;
		Config.Dmdata.RefreshToken = null;

		Logger.LogError("認可情報を失効しました");
		OnFailed(SubscribingCategories.ToArray(), false);
		SubscribingCategories.Clear();
	}

	public void Dispose()
	{
		_configSubscription?.Dispose();
		PullTimer?.Dispose();
		WebSocketReconnectTimer?.Dispose();
		TemporaryFailureRecoveryTimer?.Dispose();
		RedundantController?.Dispose();
		GC.SuppressFinalize(this);
	}

	public class DmdataTelegram : Telegram
	{
		public DmdataTelegram(
			string key,
			string title,
			string rawId,
			DateTime arrivalTime,
			DmdataRedundantTelegramPublisher publisher,
			byte[]? body = null
		) : base(key, title, rawId, arrivalTime)
		{
			BodyCache = body;
			VolatileTimer = new Timer(_ =>
			{
				BodyCache = null;
				VolatileBodyCache = body == null ? null : new(body);
				VolatileTimer = null;
			}, null, 10 * 1000, Timeout.Infinite);
			Publisher = publisher;
		}

		private Timer? VolatileTimer { get; set; }
		private byte[]? BodyCache { get; set; }
		private WeakReference<byte[]>? VolatileBodyCache { get; set; }
		private DmdataRedundantTelegramPublisher Publisher { get; }

		public override Task<Stream> GetBodyAsync()
		{
			if (BodyCache != null)
			{
				VolatileTimer?.Change(10 * 1000, Timeout.Infinite);
				return Task.FromResult<Stream>(new MemoryStream(BodyCache));
			}
			if (VolatileBodyCache?.TryGetTarget(out var cache) ?? false)
				return Task.FromResult<Stream>(new MemoryStream(cache));
			return Publisher.CacheService.TryGetOrFetchTelegramAsync(Key, () => Publisher.FetchContentAsync(Key));
		}
		public override void Cleanup() => Publisher.CacheService.DeleteTelegramCache(Key);
	}

	public class DmdataEewTelegram(DataWebSocketMessage e)
		: Telegram(e.Id, e.XmlReport!.Control.Title, e.Head.Type, e.XmlReport!.Control.DateTime)
	{
		public override Task<Stream> GetBodyAsync() => Task.FromResult(e.GetBodyStream());
		public override void Cleanup() { }
	}
}
