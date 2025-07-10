using Avalonia.Controls;
using DmdataSharp.Redundancy;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series;
using ReactiveUI;
using Splat;
using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services.TelegramPublishers.Dmdata;

public class DmdataSettingPage : ReactiveObject, ISettingPage, IDisposable
{
	public bool IsVisible => true;

	public string? Icon => null;

	public string Title => "DM-D.S.S";

	public Control DisplayControl => new DmdataPage() { DataContext = this };

	public ISettingPage[] SubPages => [];


	private ILogger Logger { get; }
	public DmdataRedundantTelegramPublisher DmdataRedundantTelegramPublisher { get; }
	public KyoshinEewViewerConfiguration Config { get; }


	private string _dmdataStatusString = "未実装です";
	public string DmdataStatusString
	{
		get => _dmdataStatusString;
		set => this.RaiseAndSetIfChanged(ref _dmdataStatusString, value);
	}

	private CancellationTokenSource? _authorizeCancellationTokenSource = null;
	public CancellationTokenSource? AuthorizeCancellationTokenSource
	{
		get => _authorizeCancellationTokenSource;
		set => this.RaiseAndSetIfChanged(ref _authorizeCancellationTokenSource, value);
	}

	private string _redundancyStatusString = "未接続";
	public string RedundancyStatusString
	{
		get => _redundancyStatusString;
		set => this.RaiseAndSetIfChanged(ref _redundancyStatusString, value);
	}

	private int _activeConnectionCount = 0;
	public int ActiveConnectionCount
	{
		get => _activeConnectionCount;
		set => this.RaiseAndSetIfChanged(ref _activeConnectionCount, value);
	}

	private string _connectedEndpointsString = "なし";
	public string ConnectedEndpointsString
	{
		get => _connectedEndpointsString;
		set => this.RaiseAndSetIfChanged(ref _connectedEndpointsString, value);
	}

	private long _totalMessagesReceived = 0;
	public long TotalMessagesReceived
	{
		get => _totalMessagesReceived;
		set => this.RaiseAndSetIfChanged(ref _totalMessagesReceived, value);
	}

	private long _duplicateMessagesFiltered = 0;
	public long DuplicateMessagesFiltered
	{
		get => _duplicateMessagesFiltered;
		set => this.RaiseAndSetIfChanged(ref _duplicateMessagesFiltered, value);
	}

	private string _lastMessageTimeString = "受信履歴なし";
	public string LastMessageTimeString
	{
		get => _lastMessageTimeString;
		set => this.RaiseAndSetIfChanged(ref _lastMessageTimeString, value);
	}

	private IDisposable? _statisticsSubscription;

	public DmdataSettingPage(
		ILogManager logManager,
		DmdataRedundantTelegramPublisher dmdataTelegramPublisher,
		KyoshinEewViewerConfiguration config)
	{
		SplatRegistrations.RegisterLazySingleton<DmdataSettingPage>();

		Logger = logManager.GetLogger<DmdataSettingPage>();
		Config = config;
		DmdataRedundantTelegramPublisher = dmdataTelegramPublisher;

		UpdateDmdataStatus();
		
		// 統計情報のイベント駆動更新
		SetupStatisticsSubscriptions();
	}

	public void CancelAuthorizeDmdata()
		=> AuthorizeCancellationTokenSource?.Cancel();

	public async Task AuthorizeDmdata()
	{
		if (AuthorizeCancellationTokenSource != null)
		{
			AuthorizeCancellationTokenSource.Cancel();
			return;
		}
		if (!string.IsNullOrEmpty(Config.Dmdata.RefreshToken))
			return;

		DmdataStatusString = "認証しています";

		AuthorizeCancellationTokenSource = new CancellationTokenSource();
		try
		{
			await DmdataRedundantTelegramPublisher.AuthorizeAsync(AuthorizeCancellationTokenSource.Token);
			DmdataStatusString = "認証成功";
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "認可フロー中に例外が発生しました");
		}
		finally
		{
			AuthorizeCancellationTokenSource = null;
		}

		UpdateDmdataStatus();
	}

	public async Task UnauthorizeDmdata()
	{
		if (string.IsNullOrEmpty(Config.Dmdata.RefreshToken))
			return;

		DmdataStatusString = "認証を解除しています";
		try
		{
			await DmdataRedundantTelegramPublisher.UnauthorizeAsync();
		}
		catch
		{
			DmdataStatusString = "トークン無効化失敗";
		}

		UpdateDmdataStatus();
	}

	private void UpdateDmdataStatus()
	{
		if (!string.IsNullOrWhiteSpace(Config.Dmdata.OAuthClientSecret))
		{
			DmdataStatusString = "クライアント資格情報フローを利用中";
			return;
		}
		if (string.IsNullOrEmpty(Config.Dmdata.RefreshToken))
		{
			DmdataStatusString = "未認証";
			return;
		}
		DmdataStatusString = "認証済み";
	}


	private void SetupStatisticsSubscriptions()
	{
		if (DmdataRedundantTelegramPublisher?.Statistics != null)
		{
			// 統計情報のプロパティ変更を監視してUI更新
			_statisticsSubscription = DmdataRedundantTelegramPublisher.Statistics
				.WhenAnyValue(
					x => x.TotalMessages,
					x => x.DuplicateMessages,
					x => x.LastMessageTime,
					x => x.AverageProcessingTime,
					x => x.MaxProcessingTime,
					x => x.EndpointStats)
				.Subscribe(_ => UpdateStatisticsFromEvent());
		}
		
		// RedundantTelegramPublisherの接続状態イベントを監視
		if (DmdataRedundantTelegramPublisher != null)
		{
			DmdataRedundantTelegramPublisher.ConnectionStatusChanged += OnConnectionStatusChanged;
		}
		
		// 初回更新
		UpdateStatisticsFromEvent();
		UpdateConnectionStatusFromEvent();
	}

	private void UpdateStatisticsFromEvent()
	{
		try
		{
			if (DmdataRedundantTelegramPublisher?.Statistics != null)
			{
				var stats = DmdataRedundantTelegramPublisher.Statistics;
				
				// 基本統計情報
				TotalMessagesReceived = stats.TotalMessages;
				DuplicateMessagesFiltered = stats.DuplicateMessages;
				
				// 接続状態
				var status = DmdataRedundantTelegramPublisher.RedundancyStatus;
				RedundancyStatusString = status switch
				{
					RedundancyStatus.FullyConnected => "完全接続",
					RedundancyStatus.PartiallyConnected => "部分接続", 
					RedundancyStatus.Degraded => "劣化",
					RedundancyStatus.Disconnected => "未接続",
					_ => status.ToString(),
				};
				
				ActiveConnectionCount = DmdataRedundantTelegramPublisher.ActiveConnectionCount;
				var endpoints = DmdataRedundantTelegramPublisher.ConnectedEndpoints;
				
				// エンドポイント情報（処理時間付き）
				if (stats.EndpointStats.Count > 0)
				{
					var endpointInfos = stats.EndpointStats
						.Where(kv => endpoints.Contains(kv.Key))
						.Select(kv => $"{kv.Key}({kv.Value.AverageProcessingTime:F1}ms)")
						.ToArray();
					ConnectedEndpointsString = endpointInfos.Length > 0 ? string.Join(", ", endpointInfos) : "なし";
				}
				else
				{
					ConnectedEndpointsString = endpoints.Length > 0 ? string.Join(", ", endpoints) : "なし";
				}
				
				// 最終受信時刻（処理時間情報付き）
				var lastTime = stats.LastMessageTime;
				if (lastTime.HasValue)
				{
					var timeInfo = $"最終受信: {lastTime.Value:yyyy/MM/dd HH:mm:ss}";
					if (stats.AverageProcessingTime > 0)
					{
						timeInfo += $" (平均処理時間: {stats.AverageProcessingTime:F1}ms)";
					}
					LastMessageTimeString = timeInfo;
				}
				else
				{
					LastMessageTimeString = "受信履歴なし";
				}
			}
			else
			{
				// 従来の統計表示にフォールバック
				UpdateStatisticsLegacy();
			}
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "統計情報の更新中にエラーが発生しました");
		}
	}

	private void UpdateStatisticsLegacy()
	{
		if (DmdataRedundantTelegramPublisher != null)
		{
			// 冗長化実装（詳細統計なし）
			var status = DmdataRedundantTelegramPublisher.RedundancyStatus;
			RedundancyStatusString = status.ToString() switch
			{
				"FullyConnected" => "完全接続",
				"PartiallyConnected" => "部分接続",
				"Degraded" => "劣化", 
				"Disconnected" => "未接続",
				_ => "不明"
			};
			
			ActiveConnectionCount = DmdataRedundantTelegramPublisher.ActiveConnectionCount;
			var endpoints = DmdataRedundantTelegramPublisher.ConnectedEndpoints;
			ConnectedEndpointsString = endpoints.Length > 0 ? string.Join(", ", endpoints) : "なし";
			
			TotalMessagesReceived = DmdataRedundantTelegramPublisher.TotalMessagesReceived;
			DuplicateMessagesFiltered = DmdataRedundantTelegramPublisher.DuplicateMessagesFiltered;
			
			var lastTime = DmdataRedundantTelegramPublisher.LastMessageTime;
			LastMessageTimeString = lastTime.HasValue 
				? $"最終受信: {lastTime.Value:yyyy/MM/dd HH:mm:ss}"
				: "受信履歴なし";
		}
		else
		{
			// 従来の実装の場合
			RedundancyStatusString = "従来実装";
			ActiveConnectionCount = 0;
			ConnectedEndpointsString = "N/A";
			TotalMessagesReceived = 0;
			DuplicateMessagesFiltered = 0;
			LastMessageTimeString = "統計情報なし";
		}
	}

	private void UpdateConnectionStatusFromEvent()
	{
		try
		{
			if (DmdataRedundantTelegramPublisher != null)
			{
				// 接続状態の更新
				var status = DmdataRedundantTelegramPublisher.RedundancyStatus;
				RedundancyStatusString = status switch
				{
					DmdataSharp.Redundancy.RedundancyStatus.FullyConnected => "完全接続",
					DmdataSharp.Redundancy.RedundancyStatus.PartiallyConnected => "部分接続",
					DmdataSharp.Redundancy.RedundancyStatus.Degraded => "劣化",
					DmdataSharp.Redundancy.RedundancyStatus.Disconnected => "未接続",
					_ => status.ToString(),
				};
				
				ActiveConnectionCount = DmdataRedundantTelegramPublisher.ActiveConnectionCount;
				var endpoints = DmdataRedundantTelegramPublisher.ConnectedEndpoints;
				ConnectedEndpointsString = endpoints.Length > 0 ? string.Join(", ", endpoints) : "なし";
				
				TotalMessagesReceived = DmdataRedundantTelegramPublisher.TotalMessagesReceived;
				DuplicateMessagesFiltered = DmdataRedundantTelegramPublisher.DuplicateMessagesFiltered;
				
				var lastTime = DmdataRedundantTelegramPublisher.LastMessageTime;
				LastMessageTimeString = lastTime.HasValue 
					? $"最終受信: {lastTime.Value:yyyy/MM/dd HH:mm:ss}"
					: "受信履歴なし";
			}
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "接続状態更新中にエラーが発生しました");
		}
	}

	private void OnConnectionStatusChanged(object? sender, EventArgs e)
	{
		UpdateConnectionStatusFromEvent();
	}

	public void Dispose()
	{
		_statisticsSubscription?.Dispose();
		if (DmdataRedundantTelegramPublisher != null)
		{
			DmdataRedundantTelegramPublisher.ConnectionStatusChanged -= OnConnectionStatusChanged;
		}
		GC.SuppressFinalize(this);
	}
}
