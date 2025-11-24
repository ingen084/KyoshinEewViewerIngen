using DmdataSharp.ApiParameters.V2;
using DmdataSharp.Interfaces;
using DmdataSharp.Redundancy;
using DmdataSharp.WebSocketMessages.V2;
using KyoshinEewViewer.Core;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DmdataSharp;

namespace KyoshinEewViewer.Services.TelegramPublishers.Dmdata;

/// <summary>
/// WebSocket/PULL接続の管理を担当する
/// </summary>
public class DmdataConnectionManager : ReactiveObject, IDisposable
{
	private ILogger Logger { get; }

	// カテゴリからカテゴリへのマップ
	private static readonly Dictionary<InformationCategory, TelegramCategoryV1> TelegramCategoryMap = new()
	{
		{ InformationCategory.Earthquake, TelegramCategoryV1.Earthquake },
		{ InformationCategory.Tsunami, TelegramCategoryV1.Earthquake },
		{ InformationCategory.Typhoon, TelegramCategoryV1.Weather },
		{ InformationCategory.EewForecast, TelegramCategoryV1.EewForecast },
		{ InformationCategory.EewWarning, TelegramCategoryV1.EewWarning },
	};

	private IRedundantDmdataSocketController? _redundantController;
	public IRedundantDmdataSocketController? RedundantController
	{
		get => _redundantController;
		private set => this.RaiseAndSetIfChanged(ref _redundantController, value);
	}

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

	/// <summary>
	/// データ受信イベント
	/// </summary>
	public event EventHandler<DataWebSocketMessage>? DataReceived;

	/// <summary>
	/// 接続状態変更イベント
	/// </summary>
	public event EventHandler? ConnectionStatusChanged;

	/// <summary>
	/// すべての接続失効イベント
	/// </summary>
	public event EventHandler? AllConnectionsLost;

	public DmdataConnectionManager(ILogManager logManager)
	{
		Logger = logManager.GetLogger<DmdataConnectionManager>();
	}

	/// <summary>
	/// WebSocket接続を開始する
	/// </summary>
	public async Task ConnectWebSocketAsync(
		IDmdataV2ApiClient apiClient,
		IEnumerable<InformationCategory> subscribingCategories,
		string appVersion,
		bool receiveTraining,
		bool useRedundancy,
		string[] customRedundantEndpoints,
		string? customDefaultEndpoint)
	{
		Logger.LogDebug("WebSocket接続処理を開始します");

		RedundantController?.Dispose();
		RedundantController = new RedundantDmdataSocketController(apiClient);
		ConfigureRedundantControllerEvents();

		var classifications = subscribingCategories.Select(c => TelegramCategoryMap[c]).Distinct().ToArray();
		if (classifications.Length <= 0)
		{
			Logger.LogInfo("取得対象が存在しないため接続しません");
			throw new InvalidOperationException("取得対象が存在しません");
		}

		var parameter = new SocketStartRequestParameter(classifications)
		{
			AppName = $"KEVi v{appVersion}",
			Types = subscribingCategories
				.Where(c => DmdataDataProcessor.GetTypesFromCategory(c).Length > 0)
				.SelectMany(c => DmdataDataProcessor.GetTypesFromCategory(c))
				.ToArray(),
			Test = receiveTraining ? "including" : "no",
		};

		// 冗長性設定に基づいてエンドポイントを選択
		string[] endpoints;
		if (useRedundancy)
		{
			// 冗長接続モード: カスタムエンドポイントまたはデフォルトの冗長エンドポイントを使用
			if (customRedundantEndpoints.Length > 0)
				endpoints = customRedundantEndpoints;
			else
				endpoints = RedundantSocketOptions.DefaultEndpoints;
		}
		else
		{
			// 単一接続モード: カスタムエンドポイントまたはグローバルエンドポイントを使用
			if (!string.IsNullOrWhiteSpace(customDefaultEndpoint))
				endpoints = [customDefaultEndpoint];
			else
				endpoints = [DmdataV2SocketEndpoints.Global];
		}

		await RedundantController.ConnectAsync(parameter, endpoints);
		UpdateConnectionStatus();
	}

	/// <summary>
	/// WebSocket接続を切断する
	/// </summary>
	public async Task DisconnectWebSocketAsync()
	{
		if (RedundantController != null)
		{
			await RedundantController.DisconnectAsync();
			UpdateConnectionStatus();
		}
	}

	/// <summary>
	/// RedundantControllerのイベントハンドラを設定する
	/// </summary>
	private void ConfigureRedundantControllerEvents()
	{
		if (RedundantController == null)
			return;

		RedundantController.DataReceived += (s, e) =>
		{
			DataReceived?.Invoke(this, e);
		};

		RedundantController.RawDataReceived += (s, e) =>
		{
			TotalMessagesReceived = RedundantController.TotalMessagesReceived;
		};

		RedundantController.AllConnectionsLost += (s, e) =>
		{
			Logger.LogWarning("すべての接続が失われました");
			UpdateConnectionStatus();
			AllConnectionsLost?.Invoke(this, EventArgs.Empty);
		};

		RedundantController.RedundancyRestored += (s, e) =>
		{
			Logger.LogInfo($"冗長性が復旧しました エンドポイント:{e.RestoredEndpoint} アクティブ接続数:{e.TotalActiveConnections}");
			UpdateConnectionStatus();
			ConnectionStatusChanged?.Invoke(this, EventArgs.Empty);
		};

		RedundantController.ConnectionError += (s, e) =>
		{
			var errorDetail = e.ErrorMessage != null
				? $"コード:{e.ErrorMessage.Code} メッセージ:{e.ErrorMessage.Error}"
				: e.Exception?.Message;
			Logger.LogWarning($"接続エラーが発生しました エンドポイント:{e.EndpointName} エラー:{errorDetail}");
			UpdateConnectionStatus();
			ConnectionStatusChanged?.Invoke(this, EventArgs.Empty);
		};

		RedundantController.ConnectionEstablished += (s, e) =>
		{
			UpdateConnectionStatus();
			ConnectionStatusChanged?.Invoke(this, EventArgs.Empty);
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
	/// WebSocketが接続されているかどうか
	/// </summary>
	public bool IsWebSocketConnected => RedundantController?.IsConnected ?? false;

	public void Dispose()
	{
		RedundantController?.Dispose();
		GC.SuppressFinalize(this);
	}
}
