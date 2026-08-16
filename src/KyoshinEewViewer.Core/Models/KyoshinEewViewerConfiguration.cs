using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Location = KyoshinMonitorLib.Location;

namespace KyoshinEewViewer.Core.Models;

/// <summary>
/// ウィンドウの位置・サイズ・状態を保存する設定
/// </summary>
public interface IWindowPlacementConfig
{
	WindowState WindowState { get; set; }
	KyoshinEewViewerConfiguration.Point2D? WindowSize { get; set; }
	KyoshinEewViewerConfiguration.Point2D? WindowLocation { get; set; }
}

public class KyoshinEewViewerConfiguration : ObservableObject, IWindowPlacementConfig
{
	private bool _showWizard = true;
	public bool ShowWizard
	{
		get => _showWizard;
		set => SetProperty(ref _showWizard, value);
	}

	private double _windowScale = 1;
	public double WindowScale
	{
		get => _windowScale;
		set => SetProperty(ref _windowScale, value);
	}

	private WindowState _windowState = WindowState.Normal;
	public WindowState WindowState
	{
		get => _windowState;
		set => SetProperty(ref _windowState, value);
	}

	private Point2D? _windowSize;
	public Point2D? WindowSize
	{
		get => _windowSize;
		set => SetProperty(ref _windowSize, value);
	}

	private Point2D? _windowLocation;
	public Point2D? WindowLocation
	{
		get => _windowLocation;
		set => SetProperty(ref _windowLocation, value);
	}

	private string? _selectedTabName;
	public string? SelectedTabName
	{
		get => _selectedTabName;
		set => SetProperty(ref _selectedTabName, value);
	}

	private bool _autoProcessPriority = false;
	public bool AutoProcessPriority
	{
		get => _autoProcessPriority;
		set => SetProperty(ref _autoProcessPriority, value);
	}

	private bool _focusExistingInstanceOnDuplicate = true;
	public bool FocusExistingInstanceOnDuplicate
	{
		get => _focusExistingInstanceOnDuplicate;
		set => SetProperty(ref _focusExistingInstanceOnDuplicate, value);
	}

	private Guid _instanceId = Guid.NewGuid();
	public Guid InstanceId
	{
		get => _instanceId;
		set => SetProperty(ref _instanceId, value);
	}

	public record Point2D(double X, double Y);

	private Version? _savedVersion;
	public Version? SavedVersion
	{
		get => _savedVersion;
		set => SetProperty(ref _savedVersion, value);
	}
	private string? _savedVersionWithSuffix;
	public string? SavedVersionWithSuffix
	{
		get => _savedVersionWithSuffix;
		set => SetProperty(ref _savedVersionWithSuffix, value);
	}

	private Dictionary<string, bool> _series = [];
	public Dictionary<string, bool> SeriesEnable
	{
		get => _series;
		set => SetProperty(ref _series, value);
	}

	private MultiWindowConfig _multiWindow = new();
	public MultiWindowConfig MultiWindow
	{
		get => _multiWindow;
		set => SetProperty(ref _multiWindow, value);
	}
	public class MultiWindowConfig : ObservableObject
	{
		private bool _enable;
		/// <summary>
		/// マルチウィンドウ機能を有効にするかどうか
		/// </summary>
		public bool Enable
		{
			get => _enable;
			set => SetProperty(ref _enable, value);
		}

		private bool _focusSubWindowOnActiveRequest;
		/// <summary>
		/// タブ切り替え要求時に分離済みSeriesのサブウィンドウをフォーカスするかどうか
		/// </summary>
		public bool FocusSubWindowOnActiveRequest
		{
			get => _focusSubWindowOnActiveRequest;
			set => SetProperty(ref _focusSubWindowOnActiveRequest, value);
		}

		private Dictionary<string, SeriesWindowConfig> _seriesWindows = [];
		/// <summary>
		/// 分離されたSeriesウィンドウの設定
		/// キー: Series.Meta.Key
		/// </summary>
		public Dictionary<string, SeriesWindowConfig> SeriesWindows
		{
			get => _seriesWindows;
			set => SetProperty(ref _seriesWindows, value);
		}
	}

	/// <summary>
	/// 分離Seriesウィンドウの設定
	/// </summary>
	public class SeriesWindowConfig : ObservableObject, IWindowPlacementConfig
	{
		private bool _isOpen = true;
		/// <summary>
		/// 前回終了時にウィンドウが開いていたかどうか
		/// </summary>
		public bool IsOpen
		{
			get => _isOpen;
			set => SetProperty(ref _isOpen, value);
		}

		private WindowState _windowState = WindowState.Normal;
		public WindowState WindowState
		{
			get => _windowState;
			set => SetProperty(ref _windowState, value);
		}

		private Point2D? _windowSize;
		public Point2D? WindowSize
		{
			get => _windowSize;
			set => SetProperty(ref _windowSize, value);
		}

		private Point2D? _windowLocation;
		public Point2D? WindowLocation
		{
			get => _windowLocation;
			set => SetProperty(ref _windowLocation, value);
		}
	}

	private TimerConfig _timer = new();
	public TimerConfig Timer
	{
		get => _timer;
		set => SetProperty(ref _timer, value);
	}
	public class TimerConfig : ObservableObject
	{
		private int _offset = 1100;
		public int Offset
		{
			get => _offset;
			set => SetProperty(ref _offset, value);
		}

		private bool _autoOffsetIncrement = true;
		public bool AutoOffsetIncrement
		{
			get => _autoOffsetIncrement;
			set => SetProperty(ref _autoOffsetIncrement, value);
		}
	}

	private KyoshinMonitorConfig _kyoshinMonitor = new();
	public KyoshinMonitorConfig KyoshinMonitor
	{
		get => _kyoshinMonitor;
		set => SetProperty(ref _kyoshinMonitor, value);
	}
	public class KyoshinMonitorConfig : ObservableObject
	{
		private KyoshinEventLevel _eventNotificationLevel = KyoshinEventLevel.Medium;
		public KyoshinEventLevel EventNotificationLevel
		{
			get => _eventNotificationLevel;
			set => SetProperty(ref _eventNotificationLevel, value);
		}

		private int _fetchFrequency = 1;
		public int FetchFrequency
		{
			get => _fetchFrequency;
			set => SetProperty(ref _fetchFrequency, value);
		}

		private bool _forcefetchOnEew;
		public bool ForcefetchOnEew
		{
			get => _forcefetchOnEew;
			set => SetProperty(ref _forcefetchOnEew, value);
		}

		private bool _forcefetchOnShakeDetect;
		public bool ForcefetchOnShakeDetect
		{
			get => _forcefetchOnShakeDetect;
			set => SetProperty(ref _forcefetchOnShakeDetect, value);
		}

		private bool _switchAtShakeDetect;
		public bool SwitchAtShakeDetect
		{
			get => _switchAtShakeDetect;
			set => SetProperty(ref _switchAtShakeDetect, value);
		}

		private bool _showColorSample = true;
		public bool ShowColorSample
		{
			get => _showColorSample;
			set => SetProperty(ref _showColorSample, value);
		}

		private bool _keepReceiveDuringReplay = true;
		public bool KeepReceiveDuringReplay
		{
			get => _keepReceiveDuringReplay;
			set => SetProperty(ref _keepReceiveDuringReplay, value);
		}

		private bool _returnToRealtimeAtShakeDetected = true;
		public bool ReturnToRealtimeAtShakeDetected
		{
			get => _returnToRealtimeAtShakeDetected;
			set => SetProperty(ref _returnToRealtimeAtShakeDetected, value);
		}

		private bool _returnToRealtimeAtEewReceived = true;
		public bool ReturnToRealtimeAtEewReceived
		{
			get => _returnToRealtimeAtEewReceived;
			set => SetProperty(ref _returnToRealtimeAtEewReceived, value);
		}

		private Mode _receiveMode = Mode.Kmoni;
		public Mode ReceiveMode
		{
			get => _receiveMode;
			set => SetProperty(ref _receiveMode, value);
		}

		private bool _autoUpdateObservationPoints = true;
		public bool AutoUpdateObservationPoints
		{
			get => _autoUpdateObservationPoints;
			set => SetProperty(ref _autoUpdateObservationPoints, value);
		}

		/// <summary>
		/// 欠損率が高い場合にレスポンス画像を保存するかどうか（ファイルに保存されない）
		/// </summary>
		private bool _saveResponseOnHighMissingRate;
		[JsonIgnore]
		public bool SaveResponseOnHighMissingRate
		{
			get => _saveResponseOnHighMissingRate;
			set => SetProperty(ref _saveResponseOnHighMissingRate, value);
		}

		public enum Mode
		{
			None = 0,
			Kmoni,
			Lmoni,
		}

		private ShakeDetectionDisplayMode _shakeDetectionDisplayMode = ShakeDetectionDisplayMode.None;
		/// <summary>
		/// 揺れ検知範囲の表示モード
		/// </summary>
		public ShakeDetectionDisplayMode ShakeDetectionDisplayMode
		{
			get => _shakeDetectionDisplayMode;
			set => SetProperty(ref _shakeDetectionDisplayMode, value);
		}

		private ShakeDetectionAnimationMode _shakeDetectionAnimationMode = ShakeDetectionAnimationMode.Blink;
		/// <summary>
		/// 揺れ検知範囲のアニメーションモード
		/// </summary>
		public ShakeDetectionAnimationMode ShakeDetectionAnimationMode
		{
			get => _shakeDetectionAnimationMode;
			set => SetProperty(ref _shakeDetectionAnimationMode, value);
		}
	}

	private EewConfig _eew = new();
	public EewConfig Eew
	{
		get => _eew;
		set => SetProperty(ref _eew, value);
	}
	public class EewConfig : ObservableObject
	{
		private bool _enableKyoshinMonitor = true;
		public bool EnableKyoshinMonitor
		{
			get => _enableKyoshinMonitor;
			set => SetProperty(ref _enableKyoshinMonitor, value);
		}

		private bool _enableSignalNowProfessional;
		public bool EnableSignalNowProfessional
		{
			get => _enableSignalNowProfessional;
			set => SetProperty(ref _enableSignalNowProfessional, value);
		}
		private bool _enableSignalNowProfessionalLocation;
		public bool EnableSignalNowProfessionalLocation
		{
			get => _enableSignalNowProfessionalLocation;
			set => SetProperty(ref _enableSignalNowProfessionalLocation, value);
		}

		private bool _showDetails;
		public bool ShowDetails
		{
			get => _showDetails;
			set => SetProperty(ref _showDetails, value);
		}

		private bool _syncKyoshinMonitorPsWave;
		public bool SyncKyoshinMonitorPsWave
		{
			get => _syncKyoshinMonitorPsWave;
			set => SetProperty(ref _syncKyoshinMonitorPsWave, value);
		}

		private bool _fillWarningArea;
		public bool FillWarningArea
		{
			get => _fillWarningArea;
			set => SetProperty(ref _fillWarningArea, value);
		}

		private bool _fillForecastIntensity;
		public bool FillForecastIntensity
		{
			get => _fillForecastIntensity;
			set => SetProperty(ref _fillForecastIntensity, value);
		}

		private bool _switchAtAnnounce;
		public bool SwitchAtAnnounce
		{
			get => _switchAtAnnounce;
			set => SetProperty(ref _switchAtAnnounce, value);
		}

		private bool _disableAnimation;
		public bool DisableAnimation
		{
			get => _disableAnimation;
			set => SetProperty(ref _disableAnimation, value);
		}

		private bool _enableExternalPointForecast;
		public bool EnableExternalPointForecast
		{
			get => _enableExternalPointForecast;
			set => SetProperty(ref _enableExternalPointForecast, value);
		}

		private bool _expandPointForecast = true;
		public bool ExpandPointForecast
		{
			get => _expandPointForecast;
			set => SetProperty(ref _expandPointForecast, value);
		}

		private KyoshinMonitorLib.JmaIntensity _pointForecastExpandIntensity = KyoshinMonitorLib.JmaIntensity.Int5Lower;
		public KyoshinMonitorLib.JmaIntensity PointForecastExpandIntensity
		{
			get => _pointForecastExpandIntensity;
			set => SetProperty(ref _pointForecastExpandIntensity, value);
		}

		private bool _showPointForecastOnMap = true;
		public bool ShowPointForecastOnMap
		{
			get => _showPointForecastOnMap;
			set => SetProperty(ref _showPointForecastOnMap, value);
		}
	}

	private ThemeConfig _theme = new();
	public ThemeConfig Theme
	{
		get => _theme;
		set => SetProperty(ref _theme, value);
	}
	public class ThemeConfig : ObservableObject
	{
		private ThemeMeta _windowTheme = new(ThemeType.BuiltIn, "Light");
		public ThemeMeta WindowTheme
		{
			get => _windowTheme;
			set => SetProperty(ref _windowTheme, value);
		}

		private ThemeMeta _intensityTheme = new(ThemeType.BuiltIn, "Standard");
		public ThemeMeta IntensityTheme
		{
			get => _intensityTheme;
			set => SetProperty(ref _intensityTheme, value);
		}

		private string? _windowThemeName = null;
		public string? WindowThemeName
		{
			get => _windowThemeName;
			set => SetProperty(ref _windowThemeName, value);
		}

		private string? _intensityThemeName = null;
		public string? IntensityThemeName
		{
			get => _intensityThemeName;
			set => SetProperty(ref _intensityThemeName, value);
		}
	}

	private NetworkTimeConfig _networkTime = new();
	public NetworkTimeConfig NetworkTime
	{
		get => _networkTime;
		set => SetProperty(ref _networkTime, value);
	}
	public class NetworkTimeConfig : ObservableObject
	{
		private bool _enable = true;
		public bool Enable
		{
			get => _enable;
			set => SetProperty(ref _enable, value);
		}
		private string _address = "time.google.com";
		public string Address
		{
			get => _address;
			set => SetProperty(ref _address, value);
		}
		private bool _enableFallbackHttp = true;
		public bool EnableFallbackHttp
		{
			get => _enableFallbackHttp;
			set => SetProperty(ref _enableFallbackHttp, value);
		}
	}

	private LoggingConfig _logging = new();
	public LoggingConfig Logging
	{
		get => _logging;
		set => SetProperty(ref _logging, value);
	}
	public class LoggingConfig : ObservableObject
	{
		private bool _enable = false;
		public bool Enable
		{
			get => _enable;
			set => SetProperty(ref _enable, value);
		}
		private string _directory = "Logs";
		public string Directory
		{
			get => _directory;
			set => SetProperty(ref _directory, value);
		}
		private bool _useCurrentDirectory = false;
		public bool UseCurrentDirectory
		{
			get => _useCurrentDirectory;
			set => SetProperty(ref _useCurrentDirectory, value);
		}
	}

	private UpdateConfig _update = new();
	public UpdateConfig Update
	{
		get => _update;
		set => SetProperty(ref _update, value);
	}
	public class UpdateConfig : ObservableObject
	{
		private bool _enable = true;
		public bool Enable
		{
			get => _enable;
			set => SetProperty(ref _enable, value);
		}

		private bool _usePreReleaseBuild = false;
		public bool UsePreReleaseBuild
		{
			get => _usePreReleaseBuild;
			set => SetProperty(ref _usePreReleaseBuild, value);
		}

		private bool _useUnstableBuild;
		public bool UseUnstableBuild
		{
			get => _useUnstableBuild;
			set => SetProperty(ref _useUnstableBuild, value);
		}

		private bool _sendCrashReport = true;
		public bool SendCrashReport
		{
			get => _sendCrashReport;
			set => SetProperty(ref _sendCrashReport, value);
		}
	}

	private NotificationConfig _notification = new();
	public NotificationConfig Notification
	{
		get => _notification;
		set => SetProperty(ref _notification, value);
	}
	public class NotificationConfig : ObservableObject
	{
		private bool _trayIconEnable = true;
		public bool TrayIconEnable
		{
			get => _trayIconEnable;
			set => SetProperty(ref _trayIconEnable, value);
		}
		private bool _hideWhenMinimizeWindow = true;
		public bool HideWhenMinimizeWindow
		{
			get => _hideWhenMinimizeWindow;
			set => SetProperty(ref _hideWhenMinimizeWindow, value);
		}
		private bool _hideWhenClosingWindow;
		public bool HideWhenClosingWindow
		{
			get => _hideWhenClosingWindow;
			set => SetProperty(ref _hideWhenClosingWindow, value);
		}

		private bool _minimizeWindowOnStartup;
		public bool MinimizeWindowOnStartup
		{
			get => _minimizeWindowOnStartup;
			set => SetProperty(ref _minimizeWindowOnStartup, value);
		}

		private bool _hideToTrayNotify = true;
		public bool HideToTrayNotify
		{
			get => _hideToTrayNotify;
			set => SetProperty(ref _hideToTrayNotify, value);
		}

		private bool _enable = true;
		public bool Enable
		{
			get => _enable;
			set => SetProperty(ref _enable, value);
		}
		private bool _switchEqSource = true;
		public bool SwitchEqSource
		{
			get => _switchEqSource;
			set => SetProperty(ref _switchEqSource, value);
		}
		private bool _gotEq = true;
		public bool GotEq
		{
			get => _gotEq;
			set => SetProperty(ref _gotEq, value);
		}
		private bool _eewReceived = true;
		public bool EewReceived
		{
			get => _eewReceived;
			set => SetProperty(ref _eewReceived, value);
		}
		private bool _tsunami = true;
		public bool Tsunami
		{
			get => _tsunami;
			set => SetProperty(ref _tsunami, value);
		}

		private bool _registerDesktopEntry = true;
		/// <summary>
		/// Linux 起動時にデスクトップエントリ (.desktop) を自動生成するかどうか
		/// </summary>
		public bool RegisterDesktopEntry
		{
			get => _registerDesktopEntry;
			set => SetProperty(ref _registerDesktopEntry, value);
		}
	}

	private MapConfig _map = new();
	public MapConfig Map
	{
		get => _map;
		set => SetProperty(ref _map, value);
	}
	public class MapConfig : ObservableObject
	{
		private bool _disableManualMapControl;
		public bool DisableManualMapControl
		{
			get => _disableManualMapControl;
			set => SetProperty(ref _disableManualMapControl, value);
		}
		private bool _keepRegion;
		public bool KeepRegion
		{
			get => _keepRegion;
			set => SetProperty(ref _keepRegion, value);
		}
		private bool _autoFocus = true;
		public bool AutoFocus
		{
			get => _autoFocus;
			set => SetProperty(ref _autoFocus, value);
		}
		private double _maxNavigateZoom = 8.5;
		public double MaxNavigateZoom
		{
			get => _maxNavigateZoom;
			set => SetProperty(ref _maxNavigateZoom, value);
		}
		private bool _showGrid = false;
		public bool ShowGrid
		{
			get => _showGrid;
			set => SetProperty(ref _showGrid, value);
		}

		private Location _location1 = new(45.619358f, 145.77399f);
		public Location Location1
		{
			get => _location1;
			set => SetProperty(ref _location1, value);
		}
		private Location _location2 = new(29.997368f, 128.22534f);
		public Location Location2
		{
			get => _location2;
			set => SetProperty(ref _location2, value);
		}

		private bool _autoFocusAnimation = true;
		public bool AutoFocusAnimation
		{
			get => _autoFocusAnimation;
			set => SetProperty(ref _autoFocusAnimation, value);
		}

		private bool _useMiniMap = true;
		public bool UseMiniMap
		{
			get => _useMiniMap;
			set => SetProperty(ref _useMiniMap, value);
		}

		private bool _isInertiaEnabled = true;
		/// <summary>
		/// 慣性スクロールを有効にするかどうか
		/// </summary>
		public bool IsInertiaEnabled
		{
			get => _isInertiaEnabled;
			set => SetProperty(ref _isInertiaEnabled, value);
		}
	}

	private DmdataConfig _dmdata = new();
	public DmdataConfig Dmdata
	{
		get => _dmdata;
		set => SetProperty(ref _dmdata, value);
	}
	public class DmdataConfig : ObservableObject
	{
		public const string DefaultOAuthClientId = "CId._xg46xWbfdrOqxN7WtwNfBUL3fhKLH9roksSfV8RV3Nj";
		private string _oAuthClientId = DefaultOAuthClientId;
		public string OAuthClientId
		{
			get => _oAuthClientId;
			set => SetProperty(ref _oAuthClientId, value);
		}
		private string? _oAuthClientSecret;
		public string? OAuthClientSecret
		{
			get => _oAuthClientSecret;
			set => SetProperty(ref _oAuthClientSecret, value);
		}
		private string? _refreshToken;
		public string? RefreshToken
		{
			get => _refreshToken;
			set => SetProperty(ref _refreshToken, value);
		}
		private bool _receiveTraining;
		public bool ReceiveTraining
		{
			get => _receiveTraining;
			set => SetProperty(ref _receiveTraining, value);
		}
		private bool _useWebSocket = true;
		public bool UseWebSocket
		{
			get => _useWebSocket;
			set => SetProperty(ref _useWebSocket, value);
		}
		private float _pullMultiply = 1;
		public float PullMultiply
		{
			get => _pullMultiply;
			set => SetProperty(ref _pullMultiply, value);
		}

		private bool _useRedundancy = false;
		public bool UseRedundancy
		{
			get => _useRedundancy;
			set => SetProperty(ref _useRedundancy, value);
		}

		// APIベースURL（UIから変更不可）
		private string? _apiBaseUrl;
		public string? ApiBaseUrl
		{
			get => _apiBaseUrl;
			set => SetProperty(ref _apiBaseUrl, value);
		}

		// データAPIベースURL（UIから変更不可）
		private string? _dataApiBaseUrl;
		public string? DataApiBaseUrl
		{
			get => _dataApiBaseUrl;
			set => SetProperty(ref _dataApiBaseUrl, value);
		}

		// WebSocketデフォルトエンドポイント（UIから変更不可）
		private string? _webSocketDefaultEndpoint;
		public string? WebSocketDefaultEndpoint
		{
			get => _webSocketDefaultEndpoint;
			set => SetProperty(ref _webSocketDefaultEndpoint, value);
		}

		// WebSocket冗長性エンドポイント（UIから変更不可）
		private string[]? _webSocketRedundantEndpoints;
		public string[]? WebSocketRedundantEndpoints
		{
			get => _webSocketRedundantEndpoints;
			set => SetProperty(ref _webSocketRedundantEndpoints, value);
		}
	}

	private EarthquakeConfig _earthquake = new();
	public EarthquakeConfig Earthquake
	{
		get => _earthquake;
		set => SetProperty(ref _earthquake, value);
	}
	public class EarthquakeConfig : ObservableObject
	{
		private bool _fillSokuhou = true;
		public bool FillSokuhou
		{
			get => _fillSokuhou;
			set => SetProperty(ref _fillSokuhou, value);
		}
		private bool _fillDetail = false;
		public bool FillDetail
		{
			get => _fillDetail;
			set => SetProperty(ref _fillDetail, value);
		}

		private bool _showHistory = true;
		public bool ShowHistory
		{
			get => _showHistory;
			set => SetProperty(ref _showHistory, value);
		}

		private bool _switchAtUpdate;
		public bool SwitchAtUpdate
		{
			get => _switchAtUpdate;
			set => SetProperty(ref _switchAtUpdate, value);
		}

		private bool _showIntensityLegend = true;
		public bool ShowIntensityLegend
		{
			get => _showIntensityLegend;
			set => SetProperty(ref _showIntensityLegend, value);
		}
	}

	private TsunamiConfig _tsunami = new();
	public TsunamiConfig Tsunami
	{
		get => _tsunami;
		set => SetProperty(ref _tsunami, value);
	}
	public class TsunamiConfig : ObservableObject
	{
		private bool _switchAtUpdate;
		public bool SwitchAtUpdate
		{
			get => _switchAtUpdate;
			set => SetProperty(ref _switchAtUpdate, value);
		}
	}

	private RadarConfig _radar = new();
	public RadarConfig Radar
	{
		get => _radar;
		set => SetProperty(ref _radar, value);
	}
	public class RadarConfig : ObservableObject
	{
		private bool _autoUpdate = true;
		public bool AutoUpdate
		{
			get => _autoUpdate;
			set => SetProperty(ref _autoUpdate, value);
		}
	}

	private RawIntensityObjectConfig _rawIntensityObject = new();
	public RawIntensityObjectConfig RawIntensityObject
	{
		get => _rawIntensityObject;
		set => SetProperty(ref _rawIntensityObject, value);
	}
	public class RawIntensityObjectConfig : ObservableObject
	{
		private double _showNameZoomLevel = 9;
		public double ShowNameZoomLevel
		{
			get => _showNameZoomLevel;
			set => SetProperty(ref _showNameZoomLevel, value);
		}

		private double _minShownIntensity = -3;
		public double MinShownIntensity
		{
			get => _minShownIntensity;
			set => SetProperty(ref _minShownIntensity, value);
		}

		private double _minShownDetailIntensity = -3;
		public double MinShownDetailIntensity
		{
			get => _minShownDetailIntensity;
			set => SetProperty(ref _minShownDetailIntensity, value);
		}

		private bool _showInvalidateIcon = true;
		public bool ShowInvalidateIcon
		{
			get => _showInvalidateIcon;
			set => SetProperty(ref _showInvalidateIcon, value);
		}
	}

	private AudioConfig _audio = new();
	public AudioConfig Audio
	{
		get => _audio;
		set => SetProperty(ref _audio, value);
	}
	public class AudioConfig : ObservableObject
	{
		private double _globalVolume = 1;
		public double GlobalVolume
		{
			get => _globalVolume;
			set => SetProperty(ref _globalVolume, value);
		}

		private bool _isMuted = false;
		public bool IsMuted
		{
			get => _isMuted;
			set => SetProperty(ref _isMuted, value);
		}

		private bool _showMuteButtonInMainWindow = true;
		public bool ShowMuteButtonInMainWindow
		{
			get => _showMuteButtonInMainWindow;
			set => SetProperty(ref _showMuteButtonInMainWindow, value);
		}
	}

	private Dictionary<string, Dictionary<string, SoundConfig>> _sounds = [];
	public Dictionary<string, Dictionary<string, SoundConfig>> Sounds
	{
		get => _sounds;
		set => SetProperty(ref _sounds, value);
	}
	public class SoundConfig : ObservableObject
	{
		private bool _enabled = false;
		public bool Enabled
		{
			get => _enabled;
			set => SetProperty(ref _enabled, value);
		}
		private string _filePath = "";
		public string FilePath
		{
			get => _filePath;
			set => SetProperty(ref _filePath, value);
		}
		private double _volume = 1;
		public double Volume
		{
			get => _volume;
			set => SetProperty(ref _volume, value);
		}
		private bool _allowMultiPlay = false;
		public bool AllowMultiPlay
		{
			get => _allowMultiPlay;
			set => SetProperty(ref _allowMultiPlay, value);
		}
	}

	private QzssConfig _qzss = new();
	public QzssConfig Qzss
	{
		get => _qzss;
		set => SetProperty(ref _qzss, value);
	}
	public class QzssConfig : ObservableObject
	{
		private bool _connect = false;
		public bool Connect
		{
			get => _connect;
			set => SetProperty(ref _connect, value);
		}

		private string _serialPort = "";
		public string SerialPort
		{
			get => _serialPort;
			set => SetProperty(ref _serialPort, value);
		}

		private int _baudRate = 115200;
		public int BaudRate
		{
			get => _baudRate;
			set => SetProperty(ref _baudRate, value);
		}

		private bool _showCurrentPositionInMap = false;
		public bool ShowCurrentPositionInMap
		{
			get => _showCurrentPositionInMap;
			set => SetProperty(ref _showCurrentPositionInMap, value);
		}

		private bool _hidePositionNumber = true;
		public bool HidePositionNumber
		{
			get => _hidePositionNumber;
			set => SetProperty(ref _hidePositionNumber, value);
		}

		private bool _ignoreOtherOrganizationReport = true;
		public bool IgnoreOtherOrganizationReport
		{
			get => _ignoreOtherOrganizationReport;
			set => SetProperty(ref _ignoreOtherOrganizationReport, value);
		}

		private bool _ignoreTrainingOrTestReport = true;
		public bool IgnoreTrainingOrTestReport
		{
			get => _ignoreTrainingOrTestReport;
			set => SetProperty(ref _ignoreTrainingOrTestReport, value);
		}

		private int _timezoneOffset = -9;
		public int TimezoneOffset
		{
			get => _timezoneOffset;
			set => SetProperty(ref _timezoneOffset, value);
		}

		// 衛星航法データ出力を有効化するメッセージを送信する
		private bool _setupSendSfrbx = true;
		public bool SetupSendSfrbx
		{
			get => _setupSendSfrbx;
			set => SetProperty(ref _setupSendSfrbx, value);
		}

		// NMEA RMC 出力を有効化するメッセージを送信する
		private bool _setupSendRmc = false;
		public bool SetupSendRmc
		{
			get => _setupSendRmc;
			set => SetProperty(ref _setupSendRmc, value);
		}

		// QZSS 信号の受信を有効化するメッセージを送信する
		private bool _setupEnableQzss = false;
		public bool SetupEnableQzss
		{
			get => _setupEnableQzss;
			set => SetProperty(ref _setupEnableQzss, value);
		}

		// 更新レート(計測間隔)を変更する
		private bool _setupChangeUpdateRate = true;
		public bool SetupChangeUpdateRate
		{
			get => _setupChangeUpdateRate;
			set => SetProperty(ref _setupChangeUpdateRate, value);
		}

		// 更新レート(ms)
		private int _setupUpdateRateMs = 200;
		public int SetupUpdateRateMs
		{
			get => _setupUpdateRateMs;
			set => SetProperty(ref _setupUpdateRateMs, value);
		}

		// ボーレートを変更する
		private bool _setupChangeBaudRate = true;
		public bool SetupChangeBaudRate
		{
			get => _setupChangeBaudRate;
			set => SetProperty(ref _setupChangeBaudRate, value);
		}

		// 設定送信時のボーレート
		private int _setupBaudRate = 115200;
		public int SetupBaudRate
		{
			get => _setupBaudRate;
			set => SetProperty(ref _setupBaudRate, value);
		}
	}

	private VoicevoxConfig _voicevox = new();
	public VoicevoxConfig Voicevox
	{
		get => _voicevox;
		set => SetProperty(ref _voicevox, value);
	}
	public class VoicevoxConfig : ObservableObject
	{
		private bool _enabled = false;
		public bool Enabled
		{
			get => _enabled;
			set => SetProperty(ref _enabled, value);
		}

		private string _address = "http://localhost:50021/";
		public string Address
		{
			get => _address;
			set => SetProperty(ref _address, value);
		}

		private int _speakerId = 2;
		public int SpeakerId
		{
			get => _speakerId;
			set => SetProperty(ref _speakerId, value);
		}

		private float _speedScale = 1;
		public float SpeedScale
		{
			get => _speedScale;
			set => SetProperty(ref _speedScale, value);
		}

		private float _pitchScale = 0;
		public float PitchScale
		{
			get => _pitchScale;
			set => SetProperty(ref _pitchScale, value);
		}

		private float _intonationScale = 1;
		public float IntonationScale
		{
			get => _intonationScale;
			set => SetProperty(ref _intonationScale, value);
		}

		private float _volumeScale = 1;
		public float VolumeScale
		{
			get => _volumeScale;
			set => SetProperty(ref _volumeScale, value);
		}

		private float _pauseLengthScale = .75f;
		public float PauseLengthScale
		{
			get => _pauseLengthScale;
			set => SetProperty(ref _pauseLengthScale, value);
		}

		private bool _clearCacheImmediately = false;
		public bool ClearCacheImmediately
		{
			get => _clearCacheImmediately;
			set => SetProperty(ref _clearCacheImmediately, value);
		}

		private bool _enableAutoCacheCleanup = true;
		public bool EnableAutoCacheCleanup
		{
			get => _enableAutoCacheCleanup;
			set => SetProperty(ref _enableAutoCacheCleanup, value);
		}

		private int _cacheMaxDays = 7;
		public int CacheMaxDays
		{
			get => _cacheMaxDays;
			set => SetProperty(ref _cacheMaxDays, value);
		}
	}

	private AxisConfig _axis = new();
	public AxisConfig Axis
	{
		get => _axis;
		set => SetProperty(ref _axis, value);
	}
	public class AxisConfig : ObservableObject
	{
		private bool _enable = false;
		public bool Enable
		{
			get => _enable;
			set => SetProperty(ref _enable, value);
		}
		private string _jwt = "";
		public string Jwt
		{
			get => _jwt;
			set => SetProperty(ref _jwt, value);
		}
	}
}

/// <summary>
/// 揺れ検知範囲の表示モード
/// </summary>
public enum ShakeDetectionDisplayMode
{
	/// <summary>
	/// 表示しない
	/// </summary>
	None,
	/// <summary>
	/// 凸包
	/// </summary>
	ConvexHull,
	/// <summary>
	/// グリッド
	/// </summary>
	Grid,
}

/// <summary>
/// 揺れ検知範囲のアニメーションモード
/// </summary>
public enum ShakeDetectionAnimationMode
{
	/// <summary>
	/// アニメーションなし
	/// </summary>
	None,
	/// <summary>
	/// 点滅
	/// </summary>
	Blink,
	/// <summary>
	/// 明滅（パルス）
	/// </summary>
	Pulse,
}
