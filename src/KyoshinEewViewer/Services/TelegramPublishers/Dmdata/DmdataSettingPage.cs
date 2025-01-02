using Avalonia.Controls;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series;
using ReactiveUI;
using Splat;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services.TelegramPublishers.Dmdata;

public class DmdataSettingPage : ReactiveObject, ISettingPage
{
	public bool IsVisible => true;

	public string? Icon => "\xf48b";

	public string Title => "DM-D.S.S";

	public Control DisplayControl => new DmdataPage() { DataContext = this };

	public ISettingPage[] SubPages => [];


	private ILogger Logger { get; }
	public DmdataTelegramPublisher DmdataTelegramPublisher { get; }
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

	public DmdataSettingPage(
		ILogManager logManager,
		DmdataTelegramPublisher dmdataTelegramPublisher,
		KyoshinEewViewerConfiguration config)
	{
		SplatRegistrations.RegisterLazySingleton<DmdataSettingPage>();

		Logger = logManager.GetLogger<DmdataSettingPage>();
		Config = config;
		DmdataTelegramPublisher = dmdataTelegramPublisher;

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
			await DmdataTelegramPublisher.AuthorizeAsync(AuthorizeCancellationTokenSource.Token);
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
			await DmdataTelegramPublisher.UnauthorizeAsync();
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
