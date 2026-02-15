using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Services.ExtarnalPublishers.P2pQuakeApi.ApiModels;
using ReactiveUI;
using Splat;
using System;
using System.Reactive.Linq;
using System.Threading;

namespace KyoshinEewViewer.Services.ExtarnalPublishers.P2pQuakeApi;

public class P2pQuakeApiInformationProvider : ReactiveObject
{
	private ILogger Logger { get; }
	private KyoshinEewViewerConfiguration Config { get; }

	private P2pQuakeApiWebSocketConnection WebSocketConnection { get; }

	private double BackoffTime { get; set; } = 1;
	private Timer ReconnectTimer { get; }

	private bool _isConnected;
	public bool IsConnected
	{
		get => _isConnected;
		private set => this.RaiseAndSetIfChanged(ref _isConnected, value);
	}

	/// <summary>
	/// この機能が求められているかどうか
	/// </summary>
	private bool IsFeatureRequired { get; set; }

	private string? _currentStatus = "待機中";
	public string? CurrentStatus
	{
		get => _currentStatus;
		private set => this.RaiseAndSetIfChanged(ref _currentStatus, value);
	}

	public event Action<P2pQuakeApiBaseMessage>? MessageReceived;

	public P2pQuakeApiInformationProvider(KyoshinEewViewerConfiguration config, ILogManager logManager)
	{
		SplatRegistrations.RegisterLazySingleton<P2pQuakeApiInformationProvider>();

		Logger = logManager.GetLogger<P2pQuakeApiInformationProvider>();
		Config = config;

		ReconnectTimer = new Timer(_ => TryReconnect(), null, Timeout.Infinite, Timeout.Infinite);

		WebSocketConnection = new P2pQuakeApiWebSocketConnection(logManager);
		WebSocketConnection.Connected += () =>
		{
			IsConnected = true;
			CurrentStatus = "接続完了";
			BackoffTime = 1;
		};
		WebSocketConnection.Error += message =>
		{
			CurrentStatus = message;
		};
		WebSocketConnection.Disconnected += () =>
		{
			IsConnected = false;
			CurrentStatus = "切断されました";
			if (Config.P2pQuakeApi.Enable)
			{
				CurrentStatus = $"切断されました。{BackoffTime:0}秒後に再接続を行います…";
				ReconnectTimer.Change(TimeSpan.FromSeconds(BackoffTime), Timeout.InfiniteTimeSpan);
			}
		};
		WebSocketConnection.MessageReceived += message =>
		{
			MessageReceived?.Invoke(message);
		};

		Config.P2pQuakeApi.WhenAnyValue(x => x.Enable).Throttle(TimeSpan.FromSeconds(1)).Subscribe(enabled =>
		{
			if (!IsFeatureRequired)
				return;

			if (enabled)
			{
				Connect();
				return;
			}
			Disconnect();
		});
	}

	public void Initialize()
	{
		if (IsFeatureRequired)
			return;

		IsFeatureRequired = true;
		if (Config.P2pQuakeApi.Enable)
			Connect();
	}

	private Random Random { get; } = new();
	private void TryReconnect()
	{
		if (!Config.P2pQuakeApi.Enable)
			return;
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
			CurrentStatus = "接続中…";
			await WebSocketConnection.ConnectAsync();
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "P2P地震情報 APIへの接続に失敗しました");
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
			ReconnectTimer.Change(Timeout.Infinite, Timeout.Infinite);
			await WebSocketConnection.DisconnectAsync();
			IsConnected = false;
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "P2P地震情報 APIの切断に失敗しました");
		}
		CurrentStatus = "切断しました";
	}
}
