using Avalonia.Controls;
using Avalonia.Platform.Storage;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.Series;
using KyoshinEewViewer.Series.Qzss.Events;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.Services.ExtarnalPublishers.Axis;
using KyoshinEewViewer.Services.Feedback;
using KyoshinEewViewer.Services.TelegramPublishers.Dmdata;
using KyoshinEewViewer.Services.Voicevox;
using KyoshinEewViewer.Services.Workflows;
using KyoshinEewViewer.Services.Workflows.BuiltinActions;
using KyoshinEewViewer.Views.SettingPages;
using KyoshinMonitorLib;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace KyoshinEewViewer.ViewModels;

public class SettingWindowViewModel : ViewModelBase
{
	public static Dictionary<KyoshinEventLevel, string> KyoshinEventLevelNames { get; } = new()
	{
		{ KyoshinEventLevel.Weaker, "微弱(非推奨)" },
		{ KyoshinEventLevel.Weak, "弱い(震度1未満)" },
		{ KyoshinEventLevel.Medium, "普通(震度1程度以上)" },
		{ KyoshinEventLevel.Strong, "強い(震度3程度以上)" },
		{ KyoshinEventLevel.Stronger, "非常に強い(震度5弱程度以上)" },
		{ KyoshinEventLevel.Disabled, "利用しない" },
	};
	public static Dictionary<KyoshinEewViewerConfiguration.KyoshinMonitorConfig.Mode, string> KyoshinMonitorModeNames { get; } = new()
	{
		{ KyoshinEewViewerConfiguration.KyoshinMonitorConfig.Mode.None, "受信しない" },
		{ KyoshinEewViewerConfiguration.KyoshinMonitorConfig.Mode.Kmoni, "強震モニタ" },
		{ KyoshinEewViewerConfiguration.KyoshinMonitorConfig.Mode.Lmoni, "長周期地震動モニタ" },
	};
	public static Dictionary<ShakeDetectionDisplayMode, string> ShakeDetectionDisplayModeNames { get; } = new()
	{
		{ ShakeDetectionDisplayMode.None, "表示しない" },
		{ ShakeDetectionDisplayMode.Grid, "グリッド" },
		{ ShakeDetectionDisplayMode.ConvexHull, "凸包(非推奨)" },
	};
	public static Dictionary<ShakeDetectionAnimationMode, string> ShakeDetectionAnimationModeNames { get; } = new()
	{
		{ ShakeDetectionAnimationMode.None, "アニメーションなし" },
		{ ShakeDetectionAnimationMode.Blink, "点滅" },
		{ ShakeDetectionAnimationMode.Pulse, "明滅" },
	};

	public KyoshinEewViewerConfiguration Config { get; }
	public SeriesController SeriesController { get; }
	public SoundPlayerService SoundPlayerService { get; }
	public UpdateCheckService UpdateCheckService { get; }
	public WorkflowService WorkflowService { get; }
	public VoicevoxService VoicevoxService { get; }
	public ISubWindowsService? SubWindowService { get; }

	private ILogger Logger { get; }

	private ISettingPage _selectedSettingPage;
	public ISettingPage SelectedSettingPage
	{
		get => _selectedSettingPage;
		set {
			var oldValue = _selectedSettingPage;
			this.RaiseAndSetIfChanged(ref _selectedSettingPage, value);
			if (value is BasicSettingPage && oldValue is not BasicSettingPage)
			{
				SelectedSettingPage = oldValue;
				return;
			}
		}
	}
	private BasicSettingPage<UpdatePage> UpdatePage { get; }
	public ISettingPage[] SettingPages { get; }

	public SettingWindowViewModel(
		KyoshinEewViewerConfiguration config,
		SeriesController seriesController,
		UpdateCheckService updateCheckService,
		SoundPlayerService soundPlayerService,
		WorkflowService workflowService,
		VoicevoxService voicevoxService,
		ILogManager logManager,
		DmdataSettingPage dmdataPage,
		AxisSettingPage axisPage,
		FeedbackSettingPage feedbackPage,
		ISubWindowsService? subWindowService)
	{
		SplatRegistrations.RegisterLazySingleton<SettingWindowViewModel>();

		Config = config;
		SeriesController = seriesController ?? throw new ArgumentNullException(nameof(seriesController));
		UpdateCheckService = updateCheckService;
		SoundPlayerService = soundPlayerService;
		WorkflowService = workflowService;
		VoicevoxService = voicevoxService;
		SubWindowService = subWindowService;

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

		updateCheckService.Updated += a =>
		{
			VersionInfos = a;
			if ((a?.Length ?? 0) > 0 && UpdatePage != null)
			{
				UpdatePage.IsVisible = true;
				SelectedSettingPage = UpdatePage;
			}
		};
		VersionInfos = updateCheckService.AvailableUpdateVersions;

		updateCheckService.WhenAnyValue(x => x.IsUpdateIndeterminate).Subscribe(x => IsUpdateIndeterminate = x);
		updateCheckService.WhenAnyValue(x => x.UpdateProgress).Subscribe(x => UpdateProgress = x);
		updateCheckService.WhenAnyValue(x => x.UpdateProgressMax).Subscribe(x => UpdateProgressMax = x);
		updateCheckService.WhenAnyValue(x => x.UpdateState).Subscribe(x => UpdateState = x);

		SelectedWorkflow = WorkflowService.Workflows.FirstOrDefault();

		VoicevoxService.WhenAnyValue(x => x.Speakers)
			.Subscribe(s => VoicevoxSpeakerName = s.SelectMany(t => t switch
			{
				MultiStyleSpeaker ms => ms.Styles,
				SingleStyleSpeaker ss => [ss],
				_ => [],
			}).FirstOrDefault(s => s.SpeakerId == config.Voicevox.SpeakerId)?.Name ?? "不明");


		UpdatePage = new BasicSettingPage<UpdatePage>("\xf071", "アプリの更新", []) { IsVisible = false };
		SettingPages = [
			UpdatePage,
			new BasicSettingPage<GeneralPage>("\xf53f", "外観･基本設定", []),
			new BasicSettingPage<FeaturePage>("\xf085", "機能設定", []),
			new BasicSettingPage<NotifyPage>("\xf075", "通知", []),
			new BasicSettingPage<MultiWindowPage>("\xf2d2", "マルチウィンドウ", []),
			new BasicSettingPage<SoundPage>("\xf028", "音声", []),
			new BasicSettingPage<WorkflowPage>("\xe289", "ワークフロー", []),
			new BasicSettingPage<VoicevoxPage>("\xf075", "VOICEVOX", []),
			..SeriesController.EnabledSeries.SelectMany(s => s.SettingPages),
			new BasicSettingPage("\xf48b", "配信サービス", [
				dmdataPage,
				axisPage,
			]),
			new BasicSettingPage<MapPage>("\xf5a0", "地図", []),
			feedbackPage,
			new BasicSettingPage<AboutPage>("\xf129", "このアプリについて", []),
			new BasicSettingPage<LicencePage>("\xf2c2", "ライセンス", []),
#if DEBUG
			new BasicSettingPage<DebugMenuPage>("\xf188", "デバッグメニュー", []),
#endif
		];
		_selectedSettingPage = SettingPages[1];
		if ((updateCheckService.AvailableUpdateVersions?.Length ?? 0) > 0)
		{
			UpdatePage.IsVisible = true;
			SelectedSettingPage = UpdatePage;
		}

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

	public SeriesViewModel[] Series { get; }

	public bool IsSoundActivated => SoundPlayerService.IsAvailable;
	public SoundConfigViewModel[] RegisteredSounds { get; }

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
		var wf = new Workflow() { Name = "新しいワークフロー", Trigger = new DummyTrigger() };
		WorkflowService.Workflows.Add(wf);
		SelectedWorkflow = wf;
	}
	public async void RemoveWorkflow(Workflow workflow)
	{
		var result = await DialogHelper.ShowSettingWindowConfirmationDialogAsync(
			"ワークフローの削除",
			$"ワークフロー「{workflow.Name}」を削除しますか？\nこの操作は元に戻すことができません。");
		
		if (result)
		{
			WorkflowService.Workflows.Remove(workflow);
			SelectedWorkflow = WorkflowService.Workflows.FirstOrDefault();
		}
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
			Logger.LogError(ex, "VOICEVOX のテスト再生中に例外が発生しました");
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
	public void ClearVoicevoxCache()
	{
		try
		{
			VoicevoxService.ClearCache();
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "VoiceVoxキャッシュのクリア中に例外が発生しました");
		}
	}

	#region Update

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

	public void ForceStartUpdater()
	{
		UpdaterEnable = false;
		IsUpdating = true;
		UpdateCheckService.StartUpdater(forceUpdate: true)
			.ContinueWith(_ => UpdaterEnable = true).ConfigureAwait(false);
	}
	#endregion

	public async Task ResetMultiWindowPositions()
	{
		var result = await DialogHelper.ShowSettingWindowConfirmationDialogAsync(
			"ウィンドウ位置のリセット",
			"すべてのウィンドウの位置設定をリセットします。\n現在開いているウィンドウは閉じられます。\nよろしいですか？");

		if (!result)
			return;

		if (Config.MultiWindow.Enable)
			SubWindowService?.CloseAllSeriesWindows();

		Config.MultiWindow.SeriesWindows.Clear();
	}

	public async Task EditWindowTheme()
	{
		if (SubWindowService == null || KyoshinEewViewerApp.Selector == null)
			return;
		await SubWindowService.ShowDialogWindowThemeEditWindow(KyoshinEewViewerApp.Selector.SelectedWindowTheme);
	}
	public async Task EditIntensityTheme()
	{
		if (SubWindowService == null || KyoshinEewViewerApp.Selector == null)
			return;
		await SubWindowService.ShowDialogIntensityThemeEditWindow(KyoshinEewViewerApp.Selector.SelectedIntensityTheme);
	}

	public bool IsLinux { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
	public bool IsWindows { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
	public bool IsMacOs { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
	public bool IsLogDirectoryCustomizable { get; } = PlatformDirectories.IsLogDirectoryCustomizable;
	public bool IsUseCurrentDirectoryOptionAvailable { get; } = !RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

	public void OpenLogDirectory()
	{
		string logPath;
		if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
		{
			logPath = PlatformDirectories.Logs;
		}
		else if (Config.Logging.UseCurrentDirectory)
		{
			logPath = Path.IsPathFullyQualified(Config.Logging.Directory)
				? Config.Logging.Directory
				: Path.Combine(Environment.CurrentDirectory, Config.Logging.Directory);
		}
		else
		{
			logPath = Path.IsPathFullyQualified(Config.Logging.Directory)
				? Config.Logging.Directory
				: Path.Combine(PlatformDirectories.ApplicationData, Config.Logging.Directory);
		}

		try
		{
			UrlOpener.OpenUrl(logPath);
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "ログディレクトリを開けませんでした");
		}
	}

	public ReactiveCommand<Unit, Unit> RegistMapPosition { get; } = ReactiveCommand.Create(() => MessageBus.Current.SendMessage(new RegistMapPositionRequested()));
	public ReactiveCommand<Unit, Unit> ResetMapPosition { get; }
	public ReactiveCommand<string, Unit> OpenUrl { get; } = ReactiveCommand.Create<string>(url => UrlOpener.OpenUrl(url));

	public ReactiveCommand<KyoshinEewViewerConfiguration.SoundConfig, Unit> OpenSoundFile { get; }

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
	{
		try
		{
			DCReport report;
			if (QzqsmHexString.StartsWith("$QZQSM"))
			{
				// NMEAセンテンスとしてパース
				report = DCReport.ParseFromNmea(QzqsmHexString);
			}
			else
			{
				// HEX文字列としてパース
				report = DCReport.Parse(Convert.FromHexString(QzqsmHexString.Length % 2 != 0 ? QzqsmHexString + "0" : QzqsmHexString));
			}
			ProcessManualDCReportRequested.Request(report);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "デバッグ用DCレポートの解析中にエラーが発生しました");
			System.Diagnostics.Debug.WriteLine($"デバッグ用DCレポートの解析中にエラーが発生しました: {ex.Message}");
		}
	}

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
