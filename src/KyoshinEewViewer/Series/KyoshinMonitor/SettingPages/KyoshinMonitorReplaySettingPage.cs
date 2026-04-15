using Avalonia.Controls;
using Avalonia.Platform.Storage;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Services;
using ReactiveUI;
using System;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.KyoshinMonitor.SettingPages;

public class KyoshinMonitorReplaySettingPage : ReactiveObject, ISettingPage
{
	public bool IsVisible => true;

	public string? Icon => null;

	public string Title => "リプレイ";

	public Control DisplayControl => new KyoshinMonitorReplayPage() { DataContext = this };

	public ISettingPage[] SubPages => [];

	public bool IsDebug { get; }
#if DEBUG
		= true;
#endif

	public KyoshinMonitorSeries Series { get; }
	public KyoshinEewViewerConfiguration Config { get; }
	private TimerService TimerService { get; }
	public ISubWindowsService? SubWindowService { get; }


	private int _timeshiftSeconds = 0;
	public int TimeshiftSeconds
	{
		get => _timeshiftSeconds;
		set {
			if (value > 10800)
				value = 10800;
			if (value < 0)
				value = 0;
			this.RaiseAndSetIfChanged(ref _timeshiftSeconds, value);
			UpdateTimeshiftString();
			TimeshiftedDateTime = TimerService.CurrentDisplayTime.AddSeconds(-TimeshiftSeconds);
		}
	}
	private string _timeshiftSecondsString = "リアルタイム";

	public KyoshinMonitorReplaySettingPage(
		KyoshinEewViewerConfiguration config,
		KyoshinMonitorSeries series,
		TimerService timerService,
		ISubWindowsService? subWindowService)
	{
		Series = series;
		Config = config;
		TimerService = timerService;
		SubWindowService = subWindowService;

		OffsetTimeshiftSeconds = ReactiveCommand.Create<string>(amountString =>
		{
			TimeshiftSeconds += int.Parse(amountString);
		});

		TimerService.DelayedTimerElapsed += t =>
		{
			TimeshiftedDateTime = t.AddSeconds(-TimeshiftSeconds);
		};
	}

	public string TimeshiftSecondsString
	{
		get => _timeshiftSecondsString;
		set => this.RaiseAndSetIfChanged(ref _timeshiftSecondsString, value);
	}
	private void UpdateTimeshiftString()
	{
		if (TimeshiftSeconds == 0)
		{
			TimeshiftSecondsString = "リアルタイム";
			return;
		}

		var sb = new StringBuilder();
		var time = TimeSpan.FromSeconds(TimeshiftSeconds);
		if (time.TotalHours >= 1)
			sb.Append((int)time.TotalHours + "時間");
		if (time.Minutes > 0)
			sb.Append(time.Minutes + "分");
		if (time.Seconds > 0)
			sb.Append(time.Seconds + "秒");
		sb.Append('前');

		TimeshiftSecondsString = sb.ToString();
	}

	private DateTime _timeshiftedDateTime;
	public DateTime TimeshiftedDateTime
	{
		get => _timeshiftedDateTime;
		set => this.RaiseAndSetIfChanged(ref _timeshiftedDateTime, value);
	}

	public ReactiveCommand<string, Unit> OffsetTimeshiftSeconds { get; }

	public async Task OpenReplayFile()
	{
		try
		{
			if (KyoshinEewViewerApp.TopLevelControl == null)
				return;
			var files = await KyoshinEewViewerApp.TopLevelControl.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
			{
				Title = "リプレイファイルを開く",
				FileTypeFilter = [FilePickerFileTypes.All],
				AllowMultiple = false,
			});
			if (files is not { Count: > 0 } || files[0].TryGetLocalPath() is not { } localPath)
				return;

			await Series.ReplayFileInformationHost.LoadAsync(localPath);
		}
		catch (Exception ex)
		{
			await ShowErrorDialog("リプレイファイルの読み込みに失敗しました", ex.Message);
		}
	}

	private async Task ShowErrorDialog(string title, string message)
	{
		var dialog = new ContentDialog
		{

			Title = title,
			Content = message,
			CloseButtonText = "OK"
		};

		await dialog.ShowAsync(SubWindowService?.SettingWindow);
	}

	private async Task ShowInfoDialog(string title, string message)
	{
		var dialog = new ContentDialog
		{
			Title = title,
			Content = message,
			CloseButtonText = "OK"
		};

		await dialog.ShowAsync();
	}
}
