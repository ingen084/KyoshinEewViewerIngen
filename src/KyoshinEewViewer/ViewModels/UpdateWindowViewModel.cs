using Avalonia.Controls;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Services;
using ReactiveUI;
using System;
using System.Threading.Tasks;

namespace KyoshinEewViewer.ViewModels;

public class UpdateWindowViewModel : ReactiveObject
{
	public string Title { get; } = "更新情報 - KyoshinEewViewer for ingen";

	public UpdateWindowViewModel()
	{
		if (Design.IsDesignMode)
		{
			VersionInfos = new VersionInfo[]
			{
					new VersionInfo
					{
						Time = DateTime.Now,
						Message = "test",
						VersionString = "1.1.31.0"
					},
			};
			IsUpdating = true;
			IsUpdateIndeterminate = false;
			UpdateProgressMax = 100;
			UpdateProgress = 50;
			return;
		}
		UpdateCheckService.Default.Updated += a =>
		{
			VersionInfos = a;
		};
		VersionInfos = UpdateCheckService.Default.AvailableUpdateVersions;

		UpdateCheckService.Default.WhenAnyValue(x => x.IsUpdateIndeterminate).Subscribe(x => IsUpdateIndeterminate = x);
		UpdateCheckService.Default.WhenAnyValue(x => x.UpdateProgress).Subscribe(x => UpdateProgress = x);
		UpdateCheckService.Default.WhenAnyValue(x => x.UpdateProgressMax).Subscribe(x => UpdateProgressMax = x);
		UpdateCheckService.Default.WhenAnyValue(x => x.UpdateState).Subscribe(x => UpdateState = x);
	}

	private VersionInfo[]? _versionInfos;
	public VersionInfo[]? VersionInfos
	{
		get => _versionInfos;
		set => this.RaiseAndSetIfChanged(ref _versionInfos, value);
	}

	private bool _updaterEnable = true;
	public bool UpdaterEnable
	{
		get => _updaterEnable;
		set => this.RaiseAndSetIfChanged(ref _updaterEnable, value);
	}

	private bool _isUpdating;
	public bool IsUpdating
	{
		get => _isUpdating;
		set => this.RaiseAndSetIfChanged(ref _isUpdating, value);
	}

	private bool _isUpdateIndeterminate;
	public bool IsUpdateIndeterminate
	{
		get => _isUpdateIndeterminate;
		set => this.RaiseAndSetIfChanged(ref _isUpdateIndeterminate, value);
	}

	private double _updateProgress;
	public double UpdateProgress
	{
		get => _updateProgress;
		set => this.RaiseAndSetIfChanged(ref _updateProgress, value);
	}

	private double _updateProgressMax;
	public double UpdateProgressMax
	{
		get => _updateProgressMax;
		set => this.RaiseAndSetIfChanged(ref _updateProgressMax, value);
	}

	private string _updateState = "-";
	public string UpdateState
	{
		get => _updateState;
		set => this.RaiseAndSetIfChanged(ref _updateState, value);
	}

	public async void StartUpdater()
	{
		UpdaterEnable = false;
		IsUpdating = true;
		await UpdateCheckService.Default.StartUpdater();
		await Task.Delay(1000);
		UpdaterEnable = true;
	}

	public void OpenUrl(string url)
		=> UrlOpener.OpenUrl(url);
}
