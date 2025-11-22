using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Services.ExtarnalPublishers.Axis.ApiModels;
using ReactiveUI;
using Splat;
using System;
using System.Reactive.Linq;
using System.Threading;

namespace KyoshinEewViewer.Services.ExtarnalPublishers.Axis;

public class AxisInformationProvider : ReactiveObject
{
	private ILogger Logger { get; }
	private KyoshinEewViewerConfiguration Config { get; }

	private AxisApiClient ApiClient { get; } = new AxisApiClient();
	private AxisWebSocketConnection WebSocketConnection { get; }

	private Timer JwtRefreshTimer { get; }

	private double BackoffTime { get; set; } = 1;
	private Timer ReconnectTimer { get; }

	private bool _isConnected;
	public bool IsConnected
	{
		get => _isConnected;
		private set => this.RaiseAndSetIfChanged(ref _isConnected, value);
	}

	/// <summary>
	/// 現時点でこの機能が求められているかどうか<br/>
	/// 求められていない場合接続しない
	/// </summary>
	private bool IsFeatureRequired { get; set; }

	private AxisJwtPayload? _currentPayload;
	public AxisJwtPayload? CurrentPayload
	{
		get => _currentPayload;
		private set => this.RaiseAndSetIfChanged(ref _currentPayload, value);
	}

	private string? _currentJwtErrorMessage;
	public string? PayloadErrorMessage
	{
		get => _currentJwtErrorMessage;
		private set => this.RaiseAndSetIfChanged(ref _currentJwtErrorMessage, value);
	}

	private string? _currentStatus = "待機中";
	public string? CurrentStatus
	{
		get => _currentStatus;
		private set => this.RaiseAndSetIfChanged(ref _currentStatus, value);
	}

	public event Action<AxisWebSocketMessage>? MessageReceived;

	public AxisInformationProvider(KyoshinEewViewerConfiguration config, ILogManager logManager)
	{
		SplatRegistrations.RegisterLazySingleton<AxisInformationProvider>();

		Logger = logManager.GetLogger<AxisInformationProvider>();
		Config = config;

		JwtRefreshTimer = new Timer(_ => CheckAndRefreshJwt(), null, Timeout.Infinite, Timeout.Infinite);
		ReconnectTimer = new Timer(_ => TryReconnect(), null, Timeout.Infinite, Timeout.Infinite);

		WebSocketConnection = new AxisWebSocketConnection(ApiClient);
		WebSocketConnection.Connected += () =>
		{
			IsConnected = true;
			CurrentStatus = "接続完了";
			BackoffTime = 1;
		};
		WebSocketConnection.PingCompleted += time =>
		{
			CurrentStatus = $"接続完了 (RTT: {time.TotalMilliseconds:0.0}ms)";
		};
		WebSocketConnection.Error += (message, canRetry) =>
		{
			CurrentStatus = message;
			if (!canRetry)
				Config.Axis.Enable = false;

		};
		WebSocketConnection.Disconnected += () =>
		{
			IsConnected = false;
			CurrentStatus = "切断されました";
			if (Config.Axis.Enable)
			{
				CurrentStatus = $"切断されました。{BackoffTime:0}秒後に再接続を行います…";
				ReconnectTimer.Change(TimeSpan.FromSeconds(BackoffTime), Timeout.InfiniteTimeSpan);
			}
		};
		WebSocketConnection.MessageReceived += message =>
		{
			Logger.LogDebug($"受信しました: {message.Channel}");
			MessageReceived?.Invoke(message);
		};

		Config.Axis.WhenAnyValue(x => x.Jwt).Subscribe(jwt =>
		{
			if (string.IsNullOrWhiteSpace(jwt))
			{
				Config.Axis.Enable = false;
				ApiClient.Jwt = null;
				CurrentPayload = null;
				PayloadErrorMessage = "トークンが入力されていません。";
				return;
			}
			try
			{
				CurrentPayload = AxisJwtPayload.Parse(jwt);
				ApiClient.Jwt = jwt;
				if (DateTimeOffset.FromUnixTimeSeconds(CurrentPayload.Exp) < DateTimeOffset.UtcNow)
				{
					Config.Axis.Enable = false;
					PayloadErrorMessage = "トークンの有効期限が切れています。";
				}
				else
					PayloadErrorMessage = null;
			}
			catch (Exception ex)
			{
				Config.Axis.Enable = false;
				CurrentPayload = null;
				ApiClient.Jwt = null;
				PayloadErrorMessage = ex.Message;
			}
		});

		Config.Axis.WhenAnyValue(x => x.Enable).Throttle(TimeSpan.FromSeconds(1)).Subscribe(enabled =>
		{
			if (!IsFeatureRequired)
				return;

			if (enabled)
			{
				Connect();
				return;
			}
			// 無効化
			Disconnect();
		});
	}

	public void Initialize()
	{
		if (IsFeatureRequired)
			return;

		IsFeatureRequired = true;
		if (Config.Axis.Enable)
			Connect();
	}

	private Random Random { get; } = new();
	private void TryReconnect()
	{
		if (!Config.Axis.Enable)
			return;
		// 1.5倍～2.5倍の間で最大1時間までバックオフタイムを伸ばしていく
		BackoffTime = Math.Min(BackoffTime * (1.5 + Random.NextDouble()), 3600);
		Connect();
	}

	private bool IsConnecting { get; set; }
	private async void Connect()
	{
		if (IsConnecting || WebSocketConnection.IsConnected)
			return;
		try
		{
			IsConnecting = true;
			// 有効化
			CurrentStatus = "接続中…";
			await WebSocketConnection.ConnectAsync();
			JwtRefreshTimer.Change(TimeSpan.FromMinutes(1), TimeSpan.FromDays(1));
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "接続に失敗しました。");
			CurrentStatus = "接続に失敗しました。自動でリトライされます…";
			ReconnectTimer.Change(TimeSpan.FromSeconds(BackoffTime), Timeout.InfiniteTimeSpan);
			IsConnected = false;
		}
		IsConnecting = false;
	}

	private async void Disconnect()
	{
		try
		{
			CurrentStatus = "切断中…";
			JwtRefreshTimer.Change(Timeout.Infinite, Timeout.Infinite);
			ReconnectTimer.Change(Timeout.Infinite, Timeout.Infinite);
			await WebSocketConnection.DisconnectAsync();
			IsConnected = false;
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "切断に失敗しました。");
		}
		CurrentStatus = "切断しました";
	}

	public async void CheckAndRefreshJwt()
	{
		if (!Config.Axis.Enable || string.IsNullOrWhiteSpace(Config.Axis.Jwt))
			return;

		try
		{
			var payload = AxisJwtPayload.Parse(Config.Axis.Jwt);
			// まだまだ余裕がある
			if (payload.ExpDateTime - DateTimeOffset.UtcNow > TimeSpan.FromDays(5))
				return;
			// 間に合わなかった
			if (payload.ExpDateTime <= DateTimeOffset.UtcNow)
			{
				PayloadErrorMessage = "トークンの有効期限が切れています。";
				Config.Axis.Enable = false;
				return;
			}

			Logger.LogInfo("トークンの有効期限が近づいているため、更新します。");
			// トークンを更新
			await ApiClient.RefreshJwt();
			Config.Axis.Jwt = ApiClient.Jwt ?? throw new Exception("新しいトークンが取得できませんでした。");

			Logger.LogInfo("トークンを更新しました。");
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "トークンの更新に失敗しました。");
		}
	}
}
