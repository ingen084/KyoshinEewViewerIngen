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
		//Config = new KyoshinEewViewerConfiguration();
		//Config.Timer.Offset = 2500;
		//Config.Theme.WindowThemeName = "Light";
		//Config.Theme.IntensityThemeName = "Standard";

		Config.Timer.WhenAnyValue(c => c.TimeshiftSeconds).Subscribe(x => UpdateTimeshiftString());
		UpdateDmdataStatus();

		//WindowThemes = App.Selector?.WindowThemes?.Select(t => t.Name).ToArray();

		if (Design.IsDesignMode)
		{
			IsDebug = true;
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

	}

	public string Title { get; } = "設定 - KyoshinEewViewer for ingen";

	private bool _isDebug;
	public bool IsDebug
	{
		get => _isDebug;
		set => this.RaiseAndSetIfChanged(ref _isDebug, value);
	}

	//private ICommand applyDmdataApiKeyCommand;
	//public ICommand ApplyDmdataApiKeyCommand => applyDmdataApiKeyCommand ??= new DelegateCommand(() => Config.Dmdata.ApiKey = DmdataApiKey);

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

	//private ICommand _registMapPositionCommand;
	//public ICommand RegistMapPositionCommand => _registMapPositionCommand ??= new DelegateCommand(() => Aggregator.GetEvent<RegistMapPositionRequested>().Publish());

	//private ICommand _resetMapPositionCommand;
	//public ICommand ResetMapPositionCommand => _resetMapPositionCommand ??= new DelegateCommand(() =>
	//{
	//	Config.Map.Location1 = new Location(24.058240f, 123.046875f);
	//	Config.Map.Location2 = new Location(45.706479f, 146.293945f);
	//});

	//[Notify]
	//public string[]? WindowThemes { get; set; }

	public Dictionary<string, string> RealtimeDataRenderModes { get; } = new()
	{
		{ nameof(RealtimeDataRenderMode.ShindoIcon), "震度アイコン" },
		{ nameof(RealtimeDataRenderMode.WideShindoIcon), "震度アイコン(ワイド)" },
		{ nameof(RealtimeDataRenderMode.RawColor), "数値変換前の色" },
		{ nameof(RealtimeDataRenderMode.ShindoIconAndRawColor), "震度アイコン+数値変換前の色" },
		{ nameof(RealtimeDataRenderMode.ShindoIconAndMonoColor), "震度アイコン+数値変換前の色(モノクロ)" },
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
	public void OffsetTimeshiftSeconds(int amount)
		=> Config.Timer.TimeshiftSeconds = Math.Clamp(Config.Timer.TimeshiftSeconds + amount, MinTimeshiftSeconds, MaxTimeshiftSeconds);
	public void BackToTimeshiftRealtime()
		=> Config.Timer.TimeshiftSeconds = 0;

	public bool IsSoundActivated => SoundPlayerService.IsAvailable;
	public IReadOnlyDictionary<SoundPlayerService.SoundCategory, List<SoundPlayerService.Sound>> RegisteredSounds => SoundPlayerService.RegisteredSounds;

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
	public CancellationTokenSource? AuthorizeCancellationTokenSource { get; set; } = null;

	public void CancelAuthorizeDmdata()
	{
		AuthorizeCancellationTokenSource?.Cancel();
	} 

	public async void AuthorizeDmdata()
	{
		if (string.IsNullOrEmpty(Config.Dmdata.RefreshToken))
		{
			DmdataStatusString = "認証しています";
			AuthorizeButtonText = "認証中";
			AuthorizeButtonEnabled = false;
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
			return;
		}

		DmdataStatusString = "認証を解除しています";
		AuthorizeButtonText = "認証解除中";
		AuthorizeButtonEnabled = false;
		try
		{
			if (DmdataTelegramPublisher.Instance is null)
				return;
			await DmdataTelegramPublisher.Instance.UnauthorizeAsync();
		}
		catch
		{
			DmdataStatusString = "認証解除失敗";
		}

		UpdateDmdataStatus();
		AuthorizeButtonEnabled = true;
	}

	private void UpdateDmdataStatus()
	{
		if (string.IsNullOrEmpty(Config.Dmdata.RefreshToken))
		{
			DmdataStatusString = "未認証です";
			AuthorizeButtonText = "認証";
		}
		else
		{
			DmdataStatusString = "認証済み";
			AuthorizeButtonText = "認証解除";
		}
	}


	public bool IsLinux { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
	public bool IsWindows { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
	public bool IsMacOS { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

	public static void RegistMapPosition() => MessageBus.Current.SendMessage(new RegistMapPositionRequested());
	public void ResetMapPosition()
	{
		Config.Map.Location1 = new KyoshinMonitorLib.Location(24.058240f, 123.046875f);
		Config.Map.Location2 = new KyoshinMonitorLib.Location(45.706479f, 146.293945f);
	}
	public static void OpenUrl(string url)
		=> UrlOpener.OpenUrl(url);

	public async Task OpenSoundFile(KyoshinEewViewerConfiguration.SoundConfig config)
	{
		if (SubWindowsService.Default.SettingWindow == null)
			return;
		var ofd = new OpenFileDialog();
		ofd.Filters.Add(new FileDialogFilter
		{
			Name = "音声ファイル",
			Extensions = new List<string>
			{
				"wav",
				"mp3",
				"ogg",
				"aiff",
			},
		});
		ofd.AllowMultiple = false;
		var files = await ofd.ShowAsync(SubWindowsService.Default.SettingWindow);
		if (files == null || files.Length <= 0 || string.IsNullOrWhiteSpace(files[0]))
			return;
		if (!File.Exists(files[0]))
			return;
		config.FilePath = files[0];
	}

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

	public static void EndDebugReplay()
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
