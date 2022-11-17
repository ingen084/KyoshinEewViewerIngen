using Avalonia.Controls;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.Services.TelegramPublishers.Dmdata;
using KyoshinMonitorLib;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KyoshinEewViewer.ViewModels;

public class SettingWindowViewModel : ViewModelBase
{
	public KyoshinEewViewerConfiguration Config { get; }

	public SettingWindowViewModel()
	{
		Config = ConfigurationService.Current;

		ResetMapPosition = ReactiveCommand.Create(() => {
			Config.Map.Location1 = new(45.61277f, 145.68626f);
			Config.Map.Location2 = new(24.168303f, 123.65456f);
		});
		OffsetTimeshiftSeconds = ReactiveCommand.Create<string>(amountString =>
		{
			var amount = int.Parse(amountString);
			Config.Timer.TimeshiftSeconds = Math.Clamp(Config.Timer.TimeshiftSeconds + amount, MinTimeshiftSeconds, MaxTimeshiftSeconds);
		});

		Config.Timer.WhenAnyValue(c => c.TimeshiftSeconds).Subscribe(x => UpdateTimeshiftString());
		UpdateDmdataStatus();

		if (Design.IsDesignMode)
		{
			IsDebug = true;
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
#if DEBUG
		IsDebug = true;
#endif

		if (RealtimeDataRenderModes.ContainsKey(ConfigurationService.Current.KyoshinMonitor.ListRenderMode))
			SelectedRealtimeDataRenderMode = RealtimeDataRenderModes.First(x => x.Key == ConfigurationService.Current.KyoshinMonitor.ListRenderMode);
		else
			SelectedRealtimeDataRenderMode = RealtimeDataRenderModes.First();

		this.WhenAnyValue(x => x.SelectedRealtimeDataRenderMode)
			.Select(x => x.Key).Subscribe(x => ConfigurationService.Current.KyoshinMonitor.ListRenderMode = x);

		UpdateCheckService.Default.Updated += a =>
		{
			VersionInfos = a;
			UpdateAvailable = a?.Any() ?? false;
		};
		VersionInfos = UpdateCheckService.Default.AvailableUpdateVersions;
		UpdateAvailable = UpdateCheckService.Default.AvailableUpdateVersions?.Any() ?? false;

		UpdateCheckService.Default.WhenAnyValue(x => x.IsUpdateIndeterminate).Subscribe(x => IsUpdateIndeterminate = x);
		UpdateCheckService.Default.WhenAnyValue(x => x.UpdateProgress).Subscribe(x => UpdateProgress = x);
		UpdateCheckService.Default.WhenAnyValue(x => x.UpdateProgressMax).Subscribe(x => UpdateProgressMax = x);
		UpdateCheckService.Default.WhenAnyValue(x => x.UpdateState).Subscribe(x => UpdateState = x);
	}

	public string Title { get; } = "設定 - KyoshinEewViewer for ingen";

	private bool _isDebug;
	public bool IsDebug
	{
		get => _isDebug;
		set => this.RaiseAndSetIfChanged(ref _isDebug, value);
	}

	public List<JmaIntensity> Ints { get; } = new List<JmaIntensity> {
		JmaIntensity.Unknown,
		JmaIntensity.Int0,
		JmaIntensity.Int1,
		JmaIntensity.Int2,
		JmaIntensity.Int3,
		JmaIntensity.Int4,
		JmaIntensity.Int5Lower,
		JmaIntensity.Int5Upper,
		JmaIntensity.Int6Lower,
		JmaIntensity.Int6Upper,
		JmaIntensity.Int7,
		JmaIntensity.Error,
	};

	public Dictionary<string, string> RealtimeDataRenderModes { get; } = new()
	{
		{ nameof(RealtimeDataRenderMode.ShindoIcon), "震度アイコン" },
		{ nameof(RealtimeDataRenderMode.WideShindoIcon), "震度アイコン(ワイド)" },
		{ nameof(RealtimeDataRenderMode.RawColor), "原色" },
		{ nameof(RealtimeDataRenderMode.ShindoIconAndRawColor), "震度アイコン+原色" },
		{ nameof(RealtimeDataRenderMode.ShindoIconAndMonoColor), "震度アイコン+原色(モノクロ)" },
	};
	private KeyValuePair<string, string> _selectedRealtimeDataRenderMode;
	public KeyValuePair<string, string> SelectedRealtimeDataRenderMode
	{
		get => _selectedRealtimeDataRenderMode;
		set => this.RaiseAndSetIfChanged(ref _selectedRealtimeDataRenderMode, value);
	}

	private int _minTimeshiftSeconds = -10800;
	public int MinTimeshiftSeconds
	{
		get => _minTimeshiftSeconds;
		set => this.RaiseAndSetIfChanged(ref _minTimeshiftSeconds, value);
	}
	private int _maxTimeshiftSeconds = 0;
	public int MaxTimeshiftSeconds
	{
		get => _maxTimeshiftSeconds;
		private set => this.RaiseAndSetIfChanged(ref _maxTimeshiftSeconds, value);
	}
	private string _timeshiftSecondsString = "リアルタイム";
	public string TimeshiftSecondsString
	{
		get => _timeshiftSecondsString;
		set => this.RaiseAndSetIfChanged(ref _timeshiftSecondsString, value);
	}
	private void UpdateTimeshiftString()
	{
		if (Config.Timer.TimeshiftSeconds == 0)
		{
			TimeshiftSecondsString = "リアルタイム";
			return;
		}

		var sb = new StringBuilder();
		var time = TimeSpan.FromSeconds(-Config.Timer.TimeshiftSeconds);
		if (time.TotalHours >= 1)
			sb.Append((int)time.TotalHours + "時間");
		if (time.Minutes > 0)
			sb.Append(time.Minutes + "分");
		if (time.Seconds > 0)
			sb.Append(time.Seconds + "秒");
		sb.Append('前');

		TimeshiftSecondsString = sb.ToString();
	}

	public ReactiveCommand<string, Unit> OffsetTimeshiftSeconds { get; }

	public void BackToTimeshiftRealtime()
		=> Config.Timer.TimeshiftSeconds = 0;

	public bool IsSoundActivated => SoundPlayerService.IsAvailable;
	public SoundConfigViewModel[] RegisteredSounds { get; }
		= SoundPlayerService.RegisteredSounds.Select(s => new SoundConfigViewModel(s.Key, s.Value)).ToArray();

	private string _dmdataStatusString = "未実装です";
	public string DmdataStatusString
	{
		get => _dmdataStatusString;
		set => this.RaiseAndSetIfChanged(ref _dmdataStatusString, value);
	}
	private string _authorizeButtonText = "認証";
	public string AuthorizeButtonText
	{
		get => _authorizeButtonText;
		set => this.RaiseAndSetIfChanged(ref _authorizeButtonText, value);
	}
	private bool _authorizeButtonEnabled = true;
	public bool AuthorizeButtonEnabled
	{
		get => _authorizeButtonEnabled;
		set => this.RaiseAndSetIfChanged(ref _authorizeButtonEnabled, value);
	}

	private CancellationTokenSource? _authorizeCancellationTokenSource = null;
	public CancellationTokenSource? AuthorizeCancellationTokenSource
	{
		get => _authorizeCancellationTokenSource;
		set => this.RaiseAndSetIfChanged(ref _authorizeCancellationTokenSource, value);
	}

	public void CancelAuthorizeDmdata()
	{
		AuthorizeCancellationTokenSource?.Cancel();
	}

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
		AuthorizeButtonText = "認証中止";

		AuthorizeCancellationTokenSource = new CancellationTokenSource();
		try
		{
			if (DmdataTelegramPublisher.Instance is null)
				return;
			await DmdataTelegramPublisher.Instance.AuthorizeAsync(AuthorizeCancellationTokenSource.Token);
			DmdataStatusString = "認証成功";
		}
		catch (Exception ex)
		{
			DmdataStatusString = "失敗 " + ex.Message;
		}
		finally
		{
			AuthorizeCancellationTokenSource = null;
		}

		UpdateDmdataStatus();
		AuthorizeButtonEnabled = true;
	}

	public async Task UnauthorizeDmdata()
	{
		if (string.IsNullOrEmpty(Config.Dmdata.RefreshToken))
			return;

		DmdataStatusString = "認証を解除しています";
		try
		{
			if (DmdataTelegramPublisher.Instance is null)
				return;
			await DmdataTelegramPublisher.Instance.UnauthorizeAsync();
		}
		catch
		{
			DmdataStatusString = "トークン無効化失敗";
		}

		UpdateDmdataStatus();
	}
	private void UpdateDmdataStatus()
	{
		if (string.IsNullOrEmpty(Config.Dmdata.RefreshToken))
		{
			DmdataStatusString = "未認証";
			AuthorizeButtonText = "認証";
		}
		else
		{
			DmdataStatusString = "認証済み";
			AuthorizeButtonText = "連携解除";
		}
	}

	#region Update

	private bool _updateAvailable = false;
	public bool UpdateAvailable
	{
		get => _updateAvailable;
		set => this.RaiseAndSetIfChanged(ref _updateAvailable, value);
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
	#endregion

	public bool IsLinux { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
	public bool IsWindows { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
	public bool IsMacOS { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

	public ReactiveCommand<Unit, Unit> RegistMapPosition { get; } = ReactiveCommand.Create(() => MessageBus.Current.SendMessage(new RegistMapPositionRequested()));
	public ReactiveCommand<Unit, Unit> ResetMapPosition { get; }
	public ReactiveCommand<string, Unit> OpenUrl { get; } = ReactiveCommand.Create<string>(url => UrlOpener.OpenUrl(url));

	public ReactiveCommand<KyoshinEewViewerConfiguration.SoundConfig, Unit> OpenSoundFile { get; } = ReactiveCommand.CreateFromTask<KyoshinEewViewerConfiguration.SoundConfig>(async config =>
	{
		if (SubWindowsService.Default.SettingWindow == null)
			return;
		var ofd = new OpenFileDialog
		{
			Filters = new()
			{
				new FileDialogFilter
				{
					Name = "音声ファイル",
					Extensions = new List<string>
					{
						"wav",
						"mp3",
						"ogg",
						"aiff",
					},
				},
			},
			AllowMultiple = false
		};
		var files = await ofd.ShowAsync(SubWindowsService.Default.SettingWindow);
		if (files == null || files.Length <= 0 || string.IsNullOrWhiteSpace(files[0]))
			return;
		if (!File.Exists(files[0]))
			return;
		config.FilePath = files[0];
	});

	private string _replayBasePath = "";
	public string ReplayBasePath
	{
		get => _replayBasePath;
		set => this.RaiseAndSetIfChanged(ref _replayBasePath, value);
	}

	private DateTimeOffset _replaySelectedDate = DateTimeOffset.Now;
	public DateTimeOffset ReplaySelectedDate
	{
		get => _replaySelectedDate;
		set => this.RaiseAndSetIfChanged(ref _replaySelectedDate, value);
	}

	private TimeSpan _replaySelectedTime;
	public TimeSpan ReplaySelectedTime
	{
		get => _replaySelectedTime;
		set => this.RaiseAndSetIfChanged(ref _replaySelectedTime, value);
	}

	public void StartDebugReplay()
		=> KyoshinMonitorReplayRequested.Request(ReplayBasePath, ReplaySelectedDate.Date + ReplaySelectedTime);

	public void EndDebugReplay()
		=> KyoshinMonitorReplayRequested.Request(null, null);

	private string _jmaEqdbId = "20180618075834";
	public string JmaEqdbId
	{
		get => _jmaEqdbId;
		set => this.RaiseAndSetIfChanged(ref _jmaEqdbId, value);
	}
	public void ProcessJmaEqdbRequest()
		=> ProcessJmaEqdbRequested.Request(JmaEqdbId);
}

public record class SoundConfigViewModel(SoundCategory Category, List<Sound> Sounds);
