using DmdataSharp;
using DmdataSharp.ApiResponses.V2.Parameters;
using DmdataSharp.Authentication.OAuth;
using DmdataSharp.Exceptions;
using DmdataSharp.Interfaces;
using DmdataSharp.WebSocketMessages.V2;
using DynamicData;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using R3;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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

	// 定数定義
	private const int MaxFailCountBeforePullSwitch = 3;
	private const int VolatileCacheRetentionSeconds = 10;
	private const double PullIntervalRandomizationFactor = 0.2;

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

	private IDmdataApiClientBuilder ClientBuilder { get; } = DmdataApiClientBuilder.Default
			.Referrer(new Uri("https://www.ingen084.net/"))
			.UserAgent($"KEVi_{Utils.Version};@ingen084");
	private OAuthCredential? Credential { get; set; }
	private IDmdataV2ApiClient? ApiClient { get; set; }

	/// <summary>
	/// 購読中のカテゴリ
	/// </summary>
	public ObservableCollection<InformationCategory> SubscribingCategories { get; } = [];

	/// <summary>
	/// 接続管理
	/// </summary>
	private DmdataConnectionManager ConnectionManager { get; }

	/// <summary>
	/// データ処理
	/// </summary>
	private DmdataDataProcessor DataProcessor { get; }

	/// <summary>
	/// 再接続戦略
	/// </summary>
	private DmdataReconnectionStrategy ReconnectionStrategy { get; }

	/// <summary>
	/// 冗長性状態
	/// </summary>
	public DmdataSharp.Redundancy.RedundancyStatus RedundancyStatus => ConnectionManager.RedundancyStatus;

	/// <summary>
	/// アクティブ接続数
	/// </summary>
	public int ActiveConnectionCount => ConnectionManager.ActiveConnectionCount;

	/// <summary>
	/// 接続中のエンドポイント
	/// </summary>
	public string[] ConnectedEndpoints => ConnectionManager.ConnectedEndpoints;

	/// <summary>
	/// 受信した総メッセージ数
	/// </summary>
	public long TotalMessagesReceived => ConnectionManager.TotalMessagesReceived;

	/// <summary>
	/// 最後にメッセージを受信した時刻
	/// </summary>
	public DateTime? LastMessageTime => ConnectionManager.LastMessageTime;

	private ILogger Logger { get; }
	private KyoshinEewViewerConfiguration Config { get; }
	private InformationCacheService CacheService { get; }

	private Random Random { get; } = new Random();
	private Timer PullTimer { get; }
	private IDisposable? _configSubscription;
	private readonly SemaphoreSlim _stateTransitionSemaphore = new(1, 1);

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
		/// 一時的な障害状態(認証情報は保持)
		/// </summary>
		TemporaryFailure
	}

	private ConnectionState _currentState = ConnectionState.Disconnected;
	private ConnectionState CurrentState
	{
		get => _currentState;
		set
		{
			PreviousState = _currentState;
			_currentState = value;
		}
	}
	private ConnectionState PreviousState { get; set; } = ConnectionState.Disconnected;
	private int FailCount { get; set; }

	public DmdataRedundantTelegramPublisher(ILogManager logManager, KyoshinEewViewerConfiguration config, InformationCacheService cacheService)
	{
		SplatRegistrations.RegisterLazySingleton<DmdataRedundantTelegramPublisher>();

		Logger = logManager.GetLogger<DmdataRedundantTelegramPublisher>();
		Config = config;
		CacheService = cacheService;

		ConnectionManager = new DmdataConnectionManager(logManager);
		DataProcessor = new DmdataDataProcessor(logManager, cacheService);
		ReconnectionStrategy = new DmdataReconnectionStrategy();

		PullTimer = new(async s => await PullFeedAsync());

		ReconnectionStrategy.InitializeWebSocketReconnectTimer(
			() => ApiClient != null &&
				SubscribingCategories.Any() &&
				Config.Dmdata.UseWebSocket &&
				ConnectionManager.RedundancyStatus == DmdataSharp.Redundancy.RedundancyStatus.Disconnected &&
				CurrentState != ConnectionState.Connecting &&
				CurrentState != ConnectionState.Disconnecting &&
				CurrentState != ConnectionState.TemporaryFailure,
			() =>
			{
				Logger.LogInfo("WebSocketへの再接続を試みます");
				_ = StartInternalAsync();
			});

		ReconnectionStrategy.InitializeTemporaryFailureRecoveryTimer(() =>
		{
			if (CurrentState == ConnectionState.TemporaryFailure && ApiClient != null)
			{
				Logger.LogInfo($"一時的な障害から復旧を試みます (試行回数: {ReconnectionStrategy.TemporaryFailureCount})");
				try
				{
					_ = StartInternalAsync();
				}
				catch (Exception ex)
				{
					Logger.LogError(ex, "一時的な障害からの復旧試行中に例外が発生しました");
				}
			}
		});

		ConnectionManager.DataReceived += async (s, e) =>
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

		ConnectionManager.AllConnectionsLost += (s, e) =>
		{
			try
			{
				FailCount++;
				ConnectionStatusChanged?.Invoke(this, EventArgs.Empty);
				if (FailCount >= MaxFailCountBeforePullSwitch)
				{
					Logger.LogInfo("接続失敗回数が上限に達したためPULL型に切り替えます");
					OnFailed(SubscribingCategories.ToArray(), true);
					_ = StartPullAsync();
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, "全接続失効イベント処理中に例外が発生しました");
			}
		};

		ConnectionManager.ConnectionStatusChanged += (s, e) =>
		{
			if (ConnectionManager.RedundancyStatus == DmdataSharp.Redundancy.RedundancyStatus.FullyConnected ||
				ConnectionManager.RedundancyStatus == DmdataSharp.Redundancy.RedundancyStatus.PartiallyConnected)
			{
				FailCount = 0;
				ReconnectionStrategy.ResetBackoffTime();
			}
			ConnectionStatusChanged?.Invoke(this, EventArgs.Empty);
		};
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

			ApiClient = BuildApiClient(Credential);
		}
		else if (!string.IsNullOrWhiteSpace(Config.Dmdata.OAuthClientSecret))
		{
			Credential = new OAuthClientCredential(
				ClientBuilder.HttpClient,
				RequiredScope,
				Config.Dmdata.OAuthClientId,
				Config.Dmdata.OAuthClientSecret);

			ApiClient = BuildApiClient(Credential);
		}
		else
			return Task.CompletedTask;

		DataProcessor.SetApiClient(ApiClient);

		// R3 の Debounce は dotnet/reactive の Throttle 相当
		_configSubscription = Observable.CombineLatest(
				Config.Dmdata.ObservePropertyChanged(x => x.UseWebSocket).AsUnitObservable(),
				Config.Dmdata.ObservePropertyChanged(x => x.ReceiveTraining).AsUnitObservable(),
				Config.Dmdata.ObservePropertyChanged(x => x.UseRedundancy).AsUnitObservable())
			.Skip(1)
			.Debounce(TimeSpan.FromSeconds(1))
			.Subscribe(async _ =>
			{
				if (ApiClient == null)
					return;
				await StartInternalAsync();
			});

		return Task.CompletedTask;
	}

	private IDmdataV2ApiClient BuildApiClient(OAuthCredential credential)
	{
		var builder = ClientBuilder.UseOAuth(credential);
		if (!string.IsNullOrWhiteSpace(Config.Dmdata.ApiBaseUrl))
			builder = builder.SetApiBaseUrl(Config.Dmdata.ApiBaseUrl);
		if (!string.IsNullOrWhiteSpace(Config.Dmdata.DataApiBaseUrl))
			builder = builder.SetDataApiBaseUrl(Config.Dmdata.DataApiBaseUrl);

		return builder.BuildV2ApiClient();
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
		DataProcessor.SetApiClient(ApiClient);
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
				if (DmdataErrorClassifier.IsAuthenticationErrorCode(contracts.Error?.Code.ToString()))
					await FailAsync();
				else
					await TemporaryFailAsync("contract.listエラー");
				return [];
			}

			var categories = contracts.Items.Where(c => c.IsValid && CategoryMap.ContainsKey(c.Classification))
				.Select(s => s.Classification)
				.SelectMany(s => CategoryMap[s]).ToArray();

			if (!Config.Dmdata.UseWebSocket)
			{
				categories = categories.Where(c => c != InformationCategory.EewForecast && c != InformationCategory.EewWarning).ToArray();
			}

			return categories;
		}
		catch (DmdataException ex)
		{
			Logger.LogError(ex, "contract.list に失敗しました");

			var errorType = DmdataErrorClassifier.ClassifyError(ex);
			if (errorType == DmdataErrorClassifier.ErrorType.Authentication)
			{
				await FailAsync();
			}
			else if (errorType == DmdataErrorClassifier.ErrorType.LocalNetwork)
			{
				Logger.LogWarning("ネットワーク障害のため契約情報を取得できません");
			}
			else
			{
				await TemporaryFailAsync("contract.listでDmdataException");
			}
			return [];
		}
	}

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
			if (!SubscribingCategories.Any())
			{
				Logger.LogInfo("取得対象が存在しないため接続しません");
				OnFailed(SubscribingCategories.ToArray(), false);
				CurrentState = ConnectionState.Disconnected;
				return;
			}

			await ConnectionManager.ConnectWebSocketAsync(
				ApiClient,
				SubscribingCategories,
				Utils.Version,
				Config.Dmdata.ReceiveTraining,
				Config.Dmdata.UseRedundancy,
				Config.Dmdata.WebSocketRedundantEndpoints ?? [],
				Config.Dmdata.WebSocketDefaultEndpoint);

			await SwitchInformationAsync(true);

			CurrentState = ConnectionState.WebSocketConnected;
		}
		catch (Exception ex)
		{
			// エラーを分類して適切な復旧戦略を選択
			var errorType = DmdataErrorClassifier.ClassifyError(ex);

			// 認証エラー: 資格情報を失効させて完全に停止
			if (errorType == DmdataErrorClassifier.ErrorType.Authentication)
			{
				Logger.LogError(ex, "WebSocket接続中に認証エラーが発生しました");
				OnFailed(SubscribingCategories.ToArray(), false);
				CurrentState = ConnectionState.Failed;
				return;
			}

			// ローカルネットワークエラー: PULL型へのフォールバックを避け、再接続を待つ
			if (errorType == DmdataErrorClassifier.ErrorType.LocalNetwork)
			{
				CurrentState = ConnectionState.Disconnected;
				Logger.LogWarning(ex, "ローカルネットワーク障害のためWebSocket接続できません");
				ReconnectionStrategy.StartWebSocketReconnectTimer();
				return;
			}

			// サービス側エラー: PULL型にフォールバック
			OnFailed(SubscribingCategories.ToArray(), true);
			CurrentState = ConnectionState.Disconnected;

			Logger.LogError(ex, "WebSocket接続中に例外が発生したためPULL型に切り替えます");
			await StartPullAsync();
		}
	}

	/// <summary>
	/// WebSocketから受信したデータを処理する
	/// </summary>
	private async Task ProcessWebSocketDataAsync(DataWebSocketMessage? e)
	{
		var result = await DataProcessor.ProcessWebSocketDataAsync(e);
		if (result == null)
			return;

		var (category, telegram) = result.Value;

		if (!SubscribingCategories.Contains(category))
			return;

		FailCount = 0;

		if (category == InformationCategory.EewForecast || category == InformationCategory.EewWarning)
		{
			var eewData = (DataWebSocketMessage)telegram;
			OnTelegramArrived(category, new DmdataEewTelegram(eewData));
			return;
		}

		dynamic telegramData = telegram;
		OnTelegramArrived(
			category,
			new DmdataTelegram(
				telegramData.Id,
				telegramData.Title,
				telegramData.Type,
				telegramData.DateTime,
				this,
				telegramData.Body
			)
		);
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

			CurrentState = ConnectionState.PullConnected;

			// サーバー負荷分散のためランダムな遅延を追加
			PullTimer.Change(TimeSpan.FromMilliseconds(interval * Math.Max(Config.Dmdata.PullMultiply, 1) * (1 + Random.NextDouble() * PullIntervalRandomizationFactor)), Timeout.InfiniteTimeSpan);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "PULL開始中にエラーが発生しました");

			// エラーを分類して適切な復旧戦略を選択
			var errorType = DmdataErrorClassifier.ClassifyError(ex);

			// 認証エラー: 資格情報を失効させて完全に停止
			if (errorType == DmdataErrorClassifier.ErrorType.Authentication)
			{
				CurrentState = ConnectionState.Failed;
				await FailAsync();
			}
			// ローカルネットワークエラー: 再接続を待つ
			else if (errorType == DmdataErrorClassifier.ErrorType.LocalNetwork)
			{
				CurrentState = ConnectionState.Disconnected;
				Logger.LogWarning("ローカルネットワーク障害のため接続できません。再接続を待機します。");
				ReconnectionStrategy.StartWebSocketReconnectTimer();
			}
			// サービス側エラー: 一時的な障害として扱い、復旧を試みる
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
		DataProcessor.ResetState();

		var interval = 1000;

		foreach (var c in SubscribingCategories)
		{
			try
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

				(var infos, interval) = await DataProcessor.FetchListAsync(c, false, Config.Dmdata.ReceiveTraining);
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
			catch (Exception ex)
			{
				Logger.LogWarning(ex, $"カテゴリ {c} の履歴取得に失敗しました");
				await Task.Delay(1000);
			}
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
			if (ConnectionManager.IsWebSocketConnected)
			{
				Logger.LogWarning("WebSocket接続中にPullしようとしました");
				return;
			}

			var (infos, interval) = await DataProcessor.FetchListAsync(null, true, Config.Dmdata.ReceiveTraining);

			foreach (var (key, title, type, arrivalTime) in Enumerable.Reverse(infos))
			{
				if (!DmdataDataProcessor.IsSubscribedType(type, SubscribingCategories))
					continue;

				var category = DmdataDataProcessor.GetCategoryFromType(type);
				if (category == null)
					continue;

				OnTelegramArrived(
					category.Value,
					new DmdataTelegram(
						key,
						title,
						type,
						arrivalTime,
						this
					)
				);
			}

			// サーバー負荷分散のためランダムな遅延を追加
			PullTimer?.Change(TimeSpan.FromMilliseconds(interval * Math.Max(Config.Dmdata.PullMultiply, 1) * (1 + Random.NextDouble() * PullIntervalRandomizationFactor)), Timeout.InfiniteTimeSpan);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "PULL受信中にエラーが発生しました");

			var errorType = DmdataErrorClassifier.ClassifyError(ex);
			if (errorType == DmdataErrorClassifier.ErrorType.Authentication)
				await FailAsync();
			else
				await TemporaryFailAsync("PULL受信エラー");
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
		// 状態遷移の競合を防ぐためセマフォで排他制御
		await _stateTransitionSemaphore.WaitAsync();
		try
		{
			if (ApiClient == null)
				throw new DmdataException("ApiClient が初期化されていません");

			// 既存の接続があれば一旦切断
			if (CurrentState == ConnectionState.WebSocketConnected ||
				CurrentState == ConnectionState.PullConnected)
			{
				CurrentState = ConnectionState.Disconnecting;
			}

			PullTimer.Change(Timeout.Infinite, Timeout.Infinite);
			ReconnectionStrategy.StopTemporaryFailureRecoveryTimer();

			await ConnectionManager.DisconnectWebSocketAsync();

			CurrentState = ConnectionState.Disconnected;

			// 設定に応じてWebSocketまたはPULL型で接続
			if (Config.Dmdata.UseWebSocket)
				await StartWebSocketAsync();
			else
				await StartPullAsync();

			// 接続成功時の復旧処理
			if (CurrentState == ConnectionState.WebSocketConnected || CurrentState == ConnectionState.PullConnected)
			{
				FailCount = 0;

				// 前の状態が障害状態だった場合は復旧とみなす
				var wasDisconnectedOrFailed = PreviousState == ConnectionState.Disconnected ||
										  PreviousState == ConnectionState.Failed ||
										  PreviousState == ConnectionState.TemporaryFailure;

				if (ReconnectionStrategy.TemporaryFailureCount > 0 || wasDisconnectedOrFailed)
				{
					if (ReconnectionStrategy.TemporaryFailureCount > 0)
					{
						Logger.LogInfo($"一時的な障害から復旧しました (試行回数: {ReconnectionStrategy.TemporaryFailureCount})");
						ReconnectionStrategy.ResetTemporaryFailure();
					}
					else
					{
						Logger.LogInfo("接続が復旧しました");
					}

					// 復旧時は優先度の高いプロバイダとして通知
					Logger.LogInfo("優先度の高いプロバイダとして復旧を通知します");
					OnInformationCategoryUpdated();

					if (Config.Dmdata.UseWebSocket)
						ReconnectionStrategy.StartWebSocketReconnectTimer();
				}
				else
				{
					Logger.LogInfo("プロバイダが利用可能になりました");
					OnInformationCategoryUpdated();
				}
			}
		}
		finally
		{
			_stateTransitionSemaphore.Release();
		}
	}

	/// <summary>
	/// WebSocket接続を即座に再接続します
	/// </summary>
	public async Task ReconnectImmediatelyAsync()
	{
		Logger.LogInfo("即時再接続が要求されました");

		if (!Config.Dmdata.UseWebSocket)
		{
			Logger.LogWarning("WebSocketモードではないため、即時再接続はスキップされます");
			return;
		}

		if (CurrentState == ConnectionState.WebSocketConnected)
		{
			Logger.LogInfo("既にWebSocketに接続されているため、再接続は不要です");
			return;
		}

		ReconnectionStrategy.ReconnectImmediately();
		await StartInternalAsync();
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

		await ConnectionManager.DisconnectWebSocketAsync();
		ApiClient = null;

		CurrentState = ConnectionState.Disconnected;
	}

	/// <summary>
	/// 一時的な障害状態に移行し、復旧を試みる
	/// </summary>
	private async Task TemporaryFailAsync(string reason)
	{
		CurrentState = ConnectionState.TemporaryFailure;
		ReconnectionStrategy.RecordTemporaryFailure();

		Logger.LogWarning($"一時的な障害が発生しました: {reason} (試行回数: {ReconnectionStrategy.TemporaryFailureCount})");

		OnFailed(SubscribingCategories.ToArray(), false);

		PullTimer.Change(Timeout.Infinite, Timeout.Infinite);
		ReconnectionStrategy.StopWebSocketReconnectTimer();
		await ConnectionManager.DisconnectWebSocketAsync();
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
		ReconnectionStrategy?.Dispose();
		ConnectionManager?.Dispose();
		_stateTransitionSemaphore?.Dispose();
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
			}, null, VolatileCacheRetentionSeconds * 1000, Timeout.Infinite);
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
				VolatileTimer?.Change(VolatileCacheRetentionSeconds * 1000, Timeout.Infinite);
				return Task.FromResult<Stream>(new MemoryStream(BodyCache));
			}
			if (VolatileBodyCache?.TryGetTarget(out var cache) ?? false)
				return Task.FromResult<Stream>(new MemoryStream(cache));
			return Publisher.CacheService.TryGetOrFetchTelegramAsync(Key, () => Publisher.DataProcessor.FetchContentAsync(Key));
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
