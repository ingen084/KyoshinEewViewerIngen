using Avalonia.Controls;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Services;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Threading.Tasks;

namespace KyoshinEewViewer.ViewModels;

public class UpdateWindowViewModel : ReactiveObject
{
	[Reactive]
	public string Title { get; set; } = "更新情報 - KyoshinEewViewer for ingen";

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

	[Reactive]
	public VersionInfo[]? VersionInfos { get; set; }

	[Reactive]
	public bool UpdaterEnable { get; set; } = true;

	[Reactive]
	public bool IsUpdating { get; set; }

	[Reactive]
	public bool IsUpdateIndeterminate { get; set; }

	[Reactive]
	public double UpdateProgress { get; set; }

	[Reactive]
	public double UpdateProgressMax { get; set; }

	[Reactive]
	public string UpdateState { get; set; } = "-";

	public async void StartUpdater()
	{
		UpdaterEnable = false;
		IsUpdating = true;
		await UpdateCheckService.Default.StartUpdater();
		await Task.Delay(1000);
		UpdaterEnable = true;
	}

	public static void OpenUrl(string url)
		=> UrlOpener.OpenUrl(url);
}
