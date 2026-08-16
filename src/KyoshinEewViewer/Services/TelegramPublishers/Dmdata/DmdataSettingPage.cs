using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using DmdataSharp.Redundancy;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series;
using R3;
using Splat;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services.TelegramPublishers.Dmdata;

public class DmdataSettingPage : ObservableObject, ISettingPage
{
	public bool IsVisible => true;

	public string? Icon => null;

	public string Title => "DM-D.S.S";

	public Control DisplayControl => new DmdataPage() { DataContext = this };

	public ISettingPage[] SubPages => [];

	private ILogger Logger { get; }
	public DmdataRedundantTelegramPublisher DmdataRedundantTelegramPublisher { get; }
	public KyoshinEewViewerConfiguration Config { get; }


	private string _dmdataStatusString = "未認証";
	public string DmdataStatusString
	{
		get => _dmdataStatusString;
		set => SetProperty(ref _dmdataStatusString, value);
	}

	private CancellationTokenSource? _authorizeCancellationTokenSource = null;
	public CancellationTokenSource? AuthorizeCancellationTokenSource
	{
		get => _authorizeCancellationTokenSource;
		set => SetProperty(ref _authorizeCancellationTokenSource, value);
	}


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
		
		// WebSocket接続状態を監視
		DmdataRedundantTelegramPublisher.ObservePropertyChanged(x => x.RedundancyStatus)
			.Subscribe(status =>
			{
				IsWebSocketConnected = status == RedundancyStatus.FullyConnected || 
				                      status == RedundancyStatus.PartiallyConnected;
			});
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

	/// <summary>
	/// WebSocket接続を即座に再接続します
	/// </summary>
	public async Task ReconnectImmediately()
	{
		try
		{
			await DmdataRedundantTelegramPublisher.ReconnectImmediatelyAsync();
			DmdataStatusString = "再接続を開始しました";
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "即時再接続に失敗しました");
			DmdataStatusString = "再接続に失敗しました";
		}
	}

	private bool _isWebSocketConnected;
	/// <summary>
	/// WebSocketが接続されているかどうかを取得します
	/// </summary>
	public bool IsWebSocketConnected
	{
		get => _isWebSocketConnected;
		private set => SetProperty(ref _isWebSocketConnected, value);
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
}
