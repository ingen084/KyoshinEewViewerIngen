using Avalonia.Controls;
using Avalonia.Platform.Storage;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.Series;
using KyoshinEewViewer.Series.Qzss.Events;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.Services.TelegramPublishers.Dmdata;
using KyoshinEewViewer.Services.Voicevox;
using KyoshinEewViewer.Services.Workflows;
using KyoshinEewViewer.Services.Workflows.BuiltinActions;
using KyoshinMonitorLib;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Ports;
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
	public SeriesController SeriesController { get; }
	public SoundPlayerService SoundPlayerService { get; }
	public DmdataTelegramPublisher DmdataTelegramPublisher { get; }
	public UpdateCheckService UpdateCheckService { get; }
	public WorkflowService WorkflowService { get; }
	public VoicevoxService VoicevoxService { get; }

	private ILogger Logger { get; }

	public SettingWindowViewModel(
		KyoshinEewViewerConfiguration config,
		SeriesController seriesController,
		UpdateCheckService updateCheckService,
		SoundPlayerService soundPlayerService,
		DmdataTelegramPublisher dmdataTelegramPublisher,
		WorkflowService workflowService,
		VoicevoxService voicevoxService,
		ILogManager logManager)
	{
		SplatRegistrations.RegisterLazySingleton<SettingWindowViewModel>();

		Config = config;
		SeriesController = seriesController ?? throw new ArgumentNullException(nameof(seriesController));
		UpdateCheckService = updateCheckService;
		SoundPlayerService = soundPlayerService;
		DmdataTelegramPublisher = dmdataTelegramPublisher;
		WorkflowService = workflowService;
		VoicevoxService = voicevoxService;

		Logger = logManager.GetLogger<SettingWindowViewModel>();

		Series = SeriesController.AllSeries.Select(s => new SeriesViewModel(s, Config)).ToArray();

		RegisteredSounds = SoundPlayerService.RegisteredSounds.Select(s => new SoundConfigViewModel(s.Key, s.Value)).ToArray();
		OpenSoundFile = ReactiveCommand.CreateFromTask<KyoshinEewViewerConfiguration.SoundConfig>(async config =>
		{
			if (KyoshinEewViewerApp.TopLevelControl == null)
				return;
			var files = await KyoshinEewViewerApp.TopLevelControl.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
			{
				Title = "音声ファイルを開く",
				FileTypeFilter = new List<FilePickerFileType>()
				{
					FilePickerFileTypes.All,
				},
				AllowMultiple = false,
			});
			if (files is not { Count: > 0 } || files[0].TryGetLocalPath() is not { } localPath)
				return;

			config.FilePath = localPath;
			return;
		});

		ResetMapPosition = ReactiveCommand.Create(() =>
		{
			Config.Map.Location1 = new(45.619358f, 145.77399f);
			Config.Map.Location2 = new(29.997368f, 128.22534f);
		});

		UpdateDmdataStatus();

		updateCheckService.Updated += a =>
		{
			VersionInfos = a;
			UpdateAvailable = (a?.Length ?? 0) > 0;
		};
		VersionInfos = updateCheckService.AvailableUpdateVersions;
		UpdateAvailable = updateCheckService.AvailableUpdateVersions?.Any() ?? false;

		updateCheckService.WhenAnyValue(x => x.IsUpdateIndeterminate).Subscribe(x => IsUpdateIndeterminate = x);
		updateCheckService.WhenAnyValue(x => x.UpdateProgress).Subscribe(x => UpdateProgress = x);
		updateCheckService.WhenAnyValue(x => x.UpdateProgressMax).Subscribe(x => UpdateProgressMax = x);
		updateCheckService.WhenAnyValue(x => x.UpdateState).Subscribe(x => UpdateState = x);

		UpdateSerialPortCommand = ReactiveCommand.Create(() => { SerialPorts = SerialPort.GetPortNames(); });

		SelectedWorkflow = WorkflowService.Workflows.FirstOrDefault();

		VoicevoxService.WhenAnyValue(x => x.Speakers)
			.Subscribe(s => VoicevoxSpeakerName = s.SelectMany(t => t switch
			{
				MultiStyleSpeaker ms => ms.Styles,
				SingleStyleSpeaker ss => [ss],
				_ => [],
			}).FirstOrDefault(s => s.SpeakerId == config.Voicevox.SpeakerId)?.Name ?? "不明");

		if (Design.IsDesignMode)
		{
			IsDebug = true;
			VersionInfos =
			[
				new VersionInfo
				{
					Time = DateTime.Now,
					Message = "test",
					VersionString = "1.1.31.0"
				},
			];
			IsUpdating = true;
			IsUpdateIndeterminate = false;
			UpdateProgressMax = 100;
			UpdateProgress = 50;
			return;
		}
#if DEBUG
		IsDebug = true;
#endif
	}

	public string Title { get; } = "設定 - KyoshinEewViewer for ingen";

	private bool _isDebug;
	public bool IsDebug
	{
		get => _isDebug;
		set => this.RaiseAndSetIfChanged(ref _isDebug, value);
	}

	public List<JmaIntensity> Ints { get; } = [
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
	];

	public List<LpgmIntensity> LpgmInts { get; } = [
		LpgmIntensity.Unknown,
		LpgmIntensity.LpgmInt0,
		LpgmIntensity.LpgmInt1,
		LpgmIntensity.LpgmInt2,
		LpgmIntensity.LpgmInt3,
		LpgmIntensity.LpgmInt4,
		LpgmIntensity.Error,
	];

	//private int _minTimeshiftSeconds = -10800;
	//public int MinTimeshiftSeconds
	//{
	//	get => _minTimeshiftSeconds;
	//	set => this.RaiseAndSetIfChanged(ref _minTimeshiftSeconds, value);
	//}
	//private int _maxTimeshiftSeconds = 0;
	//public int MaxTimeshiftSeconds
	//{
	//	get => _maxTimeshiftSeconds;
	//	private set => this.RaiseAndSetIfChanged(ref _maxTimeshiftSeconds, value);
	//}
	//private string _timeshiftSecondsString = "リアルタイム";
	//public string TimeshiftSecondsString
	//{
	//	get => _timeshiftSecondsString;
	//	set => this.RaiseAndSetIfChanged(ref _timeshiftSecondsString, value);
	//}
	//private void UpdateTimeshiftString()
	//{
	//	if (Config.Timer.TimeshiftSeconds == 0)
	//	{
	//		TimeshiftSecondsString = "リアルタイム";
	//		return;
	//	}

	//	var sb = new StringBuilder();
	//	var time = TimeSpan.FromSeconds(-Config.Timer.TimeshiftSeconds);
	//	if (time.TotalHours >= 1)
	//		sb.Append((int)time.TotalHours + "時間");
	//	if (time.Minutes > 0)
	//		sb.Append(time.Minutes + "分");
	//	if (time.Seconds > 0)
	//		sb.Append(time.Seconds + "秒");
	//	sb.Append('前');

	//	TimeshiftSecondsString = sb.ToString();
	//}

	//public ReactiveCommand<string, Unit> OffsetTimeshiftSeconds { get; }

	//public void BackToTimeshiftRealtime()
	//	=> Config.Timer.TimeshiftSeconds = 0;

	public SeriesViewModel[] Series { get; }

	public bool IsSoundActivated => SoundPlayerService.IsAvailable;
	public SoundConfigViewModel[] RegisteredSounds { get; }

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

	private Workflow? _selectedWorkflow;
	public Workflow? SelectedWorkflow
	{
		get => _selectedWorkflow;
		set => this.RaiseAndSetIfChanged(ref _selectedWorkflow, value);
	}

	public void LoadWorkflows()
	{ 
		WorkflowService.LoadWorkflows();
		SelectedWorkflow = WorkflowService.Workflows.FirstOrDefault(w => w.Id == SelectedWorkflow?.Id)
			?? WorkflowService.Workflows.FirstOrDefault();
	}
	public void AddWorkflow()
	{
		var wf = new Workflow() { Name = "新しいワークフロー", Action = new DummyAction(), Trigger = new DummyTrigger() };
		WorkflowService.Workflows.Add(wf);
		SelectedWorkflow = wf;
	}
	public void RemoveWorkflow(Workflow workflow)
	{
		WorkflowService.Workflows.Remove(workflow);
		SelectedWorkflow = WorkflowService.Workflows.FirstOrDefault();
	}
	public async Task TestRunWorkflow(Workflow workflow)
	{
		workflow.IsTestRunning = true;
		try
		{
			await workflow.TestRunAsync();
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "ワークフローのテスト実行中に例外が発生しました");
		}
		finally
		{
			workflow.IsTestRunning = false;
		}
	}
	public async Task OpenSoundFileForWorkflow(PlaySoundAction action)
	{
		if (KyoshinEewViewerApp.TopLevelControl == null)
			return;
		var files = await KyoshinEewViewerApp.TopLevelControl.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
		{
			Title = "音声ファイルを開く",
			FileTypeFilter = [FilePickerFileTypes.All],
			AllowMultiple = false,
		});
		if (files is not { Count: > 0 } || files[0].TryGetLocalPath() is not { } localPath)
			return;

		action.FilePath = localPath;
	}
	public void OpenWorkflowPage()
		=> UrlOpener.OpenUrl("https://github.com/ingen084/KyoshinEewViewerIngen/blob/develop/workflow-guide.md");

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
		AuthorizeButtonText = "認証中止";

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
		AuthorizeButtonEnabled = true;
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


	private string _voicevoxSpeakerName = "話者一覧が読み込まれていません";
	public string VoicevoxSpeakerName
	{
		get => _voicevoxSpeakerName;
		set => this.RaiseAndSetIfChanged(ref _voicevoxSpeakerName, value);
	}
	private bool _isVoicevoxTestPlaying;
	public bool IsVoicevoxTestPlaying
	{
		get => _isVoicevoxTestPlaying;
		set => this.RaiseAndSetIfChanged(ref _isVoicevoxTestPlaying, value);
	}

	public async Task PlayVoicevoxTestSound()
	{
		try
		{
			IsVoicevoxTestPlaying = true;
			await VoicevoxService.PlayTest();
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Voicevoxのテスト再生中に例外が発生しました");
		}
		finally
		{
			IsVoicevoxTestPlaying = false;
		}
	}
	public Task UpdateVoicevoxSpeakers()
		=> VoicevoxService.GetSpeakers();
	public void UpdateVoicevoxSpeaker(Speaker speaker)
	{
		if (speaker is not SingleStyleSpeaker ss)
			return;
		Config.Voicevox.SpeakerId = ss.SpeakerId;
		VoicevoxSpeakerName = ss.Name;
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

	public void StartUpdater()
	{
		UpdaterEnable = false;
		IsUpdating = true;
		UpdateCheckService.StartUpdater()
			.ContinueWith(_ => UpdaterEnable = true).ConfigureAwait(false);
	}
	#endregion

	public bool IsLinux { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
	public bool IsWindows { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
	public bool IsMacOs { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

	public ReactiveCommand<Unit, Unit> RegistMapPosition { get; } = ReactiveCommand.Create(() => MessageBus.Current.SendMessage(new RegistMapPositionRequested()));
	public ReactiveCommand<Unit, Unit> ResetMapPosition { get; }
	public ReactiveCommand<string, Unit> OpenUrl { get; } = ReactiveCommand.Create<string>(url => UrlOpener.OpenUrl(url));

	public ReactiveCommand<KyoshinEewViewerConfiguration.SoundConfig, Unit> OpenSoundFile { get; }

	private string[] _serialPorts = SerialPort.GetPortNames();
	public string[] SerialPorts
	{
		get => _serialPorts;
		set => this.RaiseAndSetIfChanged(ref _serialPorts, value);
	}
	public ReactiveCommand<Unit, Unit> UpdateSerialPortCommand { get; }

	public int[] SerialBaudRates { get; } = [4800, 9600, 19200, 38400, 57600, 115200];

	#region debug
	public string CurrentDirectory => Environment.CurrentDirectory;

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

	private string _jmaEqdbId = "20180618075834";
	public string JmaEqdbId
	{
		get => _jmaEqdbId;
		set => this.RaiseAndSetIfChanged(ref _jmaEqdbId, value);
	}
	public void ProcessJmaEqdbRequest()
		=> ProcessJmaEqdbRequested.Request(JmaEqdbId);

	private string _qzqsmHexString = "9AAF8DED25000325BA00DA4A0F5AAC5A8000000008000000200000136DCCFB40";
	public string QzqsmHexString
	{
		get => _qzqsmHexString;
		set => this.RaiseAndSetIfChanged(ref _qzqsmHexString, value);
	}

	public void ProcessDCReportRequest()
		=> ProcessManualDCReportRequested.Request(DCReport.Parse(Convert.FromHexString(QzqsmHexString)));

	public void CrashApp()
		=> throw new ApplicationException("クラッシュボタンが押下されました。");
	#endregion
}

public record class SeriesViewModel(SeriesMeta Meta, KyoshinEewViewerConfiguration Config)
{
	public bool IsEnabled
	{
		get => Config.SeriesEnable.TryGetValue(Meta.Key, out var e) ? e : Meta.IsDefaultEnabled;
		set => Config.SeriesEnable[Meta.Key] = value;
	}
}

public record class SoundConfigViewModel(SoundCategory Category, List<Sound> Sounds);
