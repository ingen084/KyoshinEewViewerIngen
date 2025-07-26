using Avalonia.Controls;
using DmdataSharp.Redundancy;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series;
using ReactiveUI;
using Splat;
using System;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services.TelegramPublishers.Dmdata;

public class DmdataSettingPage : ReactiveObject, ISettingPage
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
		set => this.RaiseAndSetIfChanged(ref _dmdataStatusString, value);
	}

	private CancellationTokenSource? _authorizeCancellationTokenSource = null;
	public CancellationTokenSource? AuthorizeCancellationTokenSource
	{
		get => _authorizeCancellationTokenSource;
		set => this.RaiseAndSetIfChanged(ref _authorizeCancellationTokenSource, value);
	}

	private int _activeConnectionCount = 0;
	public int ActiveConnectionCount
	{
		get => _activeConnectionCount;
		set => this.RaiseAndSetIfChanged(ref _activeConnectionCount, value);
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
}
