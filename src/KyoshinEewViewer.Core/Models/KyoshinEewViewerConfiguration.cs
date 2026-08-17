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

public partial class KyoshinEewViewerConfiguration : ObservableObject, IWindowPlacementConfig
{
	[ObservableProperty]
	public partial bool ShowWizard { get; set; } = true;

	[ObservableProperty]
	public partial double WindowScale { get; set; } = 1;

	[ObservableProperty]
	public partial WindowState WindowState { get; set; } = WindowState.Normal;

	[ObservableProperty]
	public partial Point2D? WindowSize { get; set; }

	[ObservableProperty]
	public partial Point2D? WindowLocation { get; set; }

	[ObservableProperty]
	public partial string? SelectedTabName { get; set; }

	[ObservableProperty]
	public partial bool AutoProcessPriority { get; set; } = false;

	[ObservableProperty]
	public partial bool FocusExistingInstanceOnDuplicate { get; set; } = true;

	[ObservableProperty]
	public partial Guid InstanceId { get; set; } = Guid.NewGuid();

	public record Point2D(double X, double Y);

	[ObservableProperty]
	public partial Version? SavedVersion { get; set; }
	[ObservableProperty]
	public partial string? SavedVersionWithSuffix { get; set; }

	[ObservableProperty]
	public partial Dictionary<string, bool> SeriesEnable { get; set; } = [];

	[ObservableProperty]
	public partial MultiWindowConfig MultiWindow { get; set; } = new();
	public partial class MultiWindowConfig : ObservableObject
	{
		/// <summary>
		/// マルチウィンドウ機能を有効にするかどうか
		/// </summary>
		[ObservableProperty]
		public partial bool Enable { get; set; }

		/// <summary>
		/// タブ切り替え要求時に分離済みSeriesのサブウィンドウをフォーカスするかどうか
		/// </summary>
		[ObservableProperty]
		public partial bool FocusSubWindowOnActiveRequest { get; set; }

		/// <summary>
		/// 分離されたSeriesウィンドウの設定
		/// キー: Series.Meta.Key
		/// </summary>
		[ObservableProperty]
		public partial Dictionary<string, SeriesWindowConfig> SeriesWindows { get; set; } = [];
	}

	/// <summary>
	/// 分離Seriesウィンドウの設定
	/// </summary>
	public partial class SeriesWindowConfig : ObservableObject, IWindowPlacementConfig
	{
		/// <summary>
		/// 前回終了時にウィンドウが開いていたかどうか
		/// </summary>
		[ObservableProperty]
		public partial bool IsOpen { get; set; } = true;

		[ObservableProperty]
		public partial WindowState WindowState { get; set; } = WindowState.Normal;

		[ObservableProperty]
		public partial Point2D? WindowSize { get; set; }

		[ObservableProperty]
		public partial Point2D? WindowLocation { get; set; }
	}

	[ObservableProperty]
	public partial TimerConfig Timer { get; set; } = new();
	public partial class TimerConfig : ObservableObject
	{
		[ObservableProperty]
		public partial int Offset { get; set; } = 1100;

		[ObservableProperty]
		public partial bool AutoOffsetIncrement { get; set; } = true;
	}

	[ObservableProperty]
	public partial KyoshinMonitorConfig KyoshinMonitor { get; set; } = new();
	public partial class KyoshinMonitorConfig : ObservableObject
	{
		[ObservableProperty]
		public partial KyoshinEventLevel EventNotificationLevel { get; set; } = KyoshinEventLevel.Medium;

		[ObservableProperty]
		public partial int FetchFrequency { get; set; } = 1;

		[ObservableProperty]
		public partial bool ForcefetchOnEew { get; set; }

		[ObservableProperty]
		public partial bool ForcefetchOnShakeDetect { get; set; }

		[ObservableProperty]
		public partial bool SwitchAtShakeDetect { get; set; }

		[ObservableProperty]
		public partial bool ShowColorSample { get; set; } = true;

		[ObservableProperty]
		public partial bool KeepReceiveDuringReplay { get; set; } = true;

		[ObservableProperty]
		public partial bool ReturnToRealtimeAtShakeDetected { get; set; } = true;

		[ObservableProperty]
		public partial bool ReturnToRealtimeAtEewReceived { get; set; } = true;

		[ObservableProperty]
		public partial Mode ReceiveMode { get; set; } = Mode.Kmoni;

		[ObservableProperty]
		public partial bool AutoUpdateObservationPoints { get; set; } = true;

		/// <summary>
		/// 欠損率が高い場合にレスポンス画像を保存するかどうか（ファイルに保存されない）
		/// </summary>
		[JsonIgnore]
		[ObservableProperty]
		public partial bool SaveResponseOnHighMissingRate { get; set; }

		public enum Mode
		{
			None = 0,
			Kmoni,
			Lmoni,
		}

		/// <summary>
		/// 揺れ検知範囲の表示モード
		/// </summary>
		[ObservableProperty]
		public partial ShakeDetectionDisplayMode ShakeDetectionDisplayMode { get; set; } = ShakeDetectionDisplayMode.None;

		/// <summary>
		/// 揺れ検知範囲のアニメーションモード
		/// </summary>
		[ObservableProperty]
		public partial ShakeDetectionAnimationMode ShakeDetectionAnimationMode { get; set; } = ShakeDetectionAnimationMode.Blink;
	}

	[ObservableProperty]
	public partial EewConfig Eew { get; set; } = new();
	public partial class EewConfig : ObservableObject
	{
		[ObservableProperty]
		public partial bool EnableKyoshinMonitor { get; set; } = true;

		[ObservableProperty]
		public partial bool EnableSignalNowProfessional { get; set; }
		[ObservableProperty]
		public partial bool EnableSignalNowProfessionalLocation { get; set; }

		[ObservableProperty]
		public partial bool ShowDetails { get; set; }

		[ObservableProperty]
		public partial bool SyncKyoshinMonitorPsWave { get; set; }

		[ObservableProperty]
		public partial bool FillWarningArea { get; set; }

		[ObservableProperty]
		public partial bool FillForecastIntensity { get; set; }

		[ObservableProperty]
		public partial bool SwitchAtAnnounce { get; set; }

		[ObservableProperty]
		public partial bool DisableAnimation { get; set; }

		[ObservableProperty]
		public partial bool EnableExternalPointForecast { get; set; }

		[ObservableProperty]
		public partial bool ExpandPointForecast { get; set; } = true;

		[ObservableProperty]
		public partial KyoshinMonitorLib.JmaIntensity PointForecastExpandIntensity { get; set; } = KyoshinMonitorLib.JmaIntensity.Int5Lower;

		[ObservableProperty]
		public partial bool ShowPointForecastOnMap { get; set; } = true;
	}

	[ObservableProperty]
	public partial ThemeConfig Theme { get; set; } = new();
	public partial class ThemeConfig : ObservableObject
	{
		[ObservableProperty]
		public partial ThemeMeta WindowTheme { get; set; } = new(ThemeType.BuiltIn, "Light");

		[ObservableProperty]
		public partial ThemeMeta IntensityTheme { get; set; } = new(ThemeType.BuiltIn, "Standard");

		[ObservableProperty]
		public partial string? WindowThemeName { get; set; } = null;

		[ObservableProperty]
		public partial string? IntensityThemeName { get; set; } = null;
	}

	[ObservableProperty]
	public partial NetworkTimeConfig NetworkTime { get; set; } = new();
	public partial class NetworkTimeConfig : ObservableObject
	{
		[ObservableProperty]
		public partial bool Enable { get; set; } = true;
		[ObservableProperty]
		public partial string Address { get; set; } = "time.google.com";
		[ObservableProperty]
		public partial bool EnableFallbackHttp { get; set; } = true;
	}

	[ObservableProperty]
	public partial LoggingConfig Logging { get; set; } = new();
	public partial class LoggingConfig : ObservableObject
	{
		[ObservableProperty]
		public partial bool Enable { get; set; } = false;
		[ObservableProperty]
		public partial string Directory { get; set; } = "Logs";
		[ObservableProperty]
		public partial bool UseCurrentDirectory { get; set; } = false;
	}

	[ObservableProperty]
	public partial UpdateConfig Update { get; set; } = new();
	public partial class UpdateConfig : ObservableObject
	{
		[ObservableProperty]
		public partial bool Enable { get; set; } = true;

		[ObservableProperty]
		public partial bool UsePreReleaseBuild { get; set; } = false;

		[ObservableProperty]
		public partial bool UseUnstableBuild { get; set; }

		[ObservableProperty]
		public partial bool SendCrashReport { get; set; } = true;
	}

	[ObservableProperty]
	public partial NotificationConfig Notification { get; set; } = new();
	public partial class NotificationConfig : ObservableObject
	{
		[ObservableProperty]
		public partial bool TrayIconEnable { get; set; } = true;
		[ObservableProperty]
		public partial bool HideWhenMinimizeWindow { get; set; } = true;
		[ObservableProperty]
		public partial bool HideWhenClosingWindow { get; set; }

		[ObservableProperty]
		public partial bool MinimizeWindowOnStartup { get; set; }

		[ObservableProperty]
		public partial bool HideToTrayNotify { get; set; } = true;

		[ObservableProperty]
		public partial bool Enable { get; set; } = true;
		[ObservableProperty]
		public partial bool SwitchEqSource { get; set; } = true;
		[ObservableProperty]
		public partial bool GotEq { get; set; } = true;
		[ObservableProperty]
		public partial bool EewReceived { get; set; } = true;
		[ObservableProperty]
		public partial bool Tsunami { get; set; } = true;

		/// <summary>
		/// Linux 起動時にデスクトップエントリ (.desktop) を自動生成するかどうか
		/// </summary>
		[ObservableProperty]
		public partial bool RegisterDesktopEntry { get; set; } = true;
	}

	[ObservableProperty]
	public partial MapConfig Map { get; set; } = new();
	public partial class MapConfig : ObservableObject
	{
		[ObservableProperty]
		public partial bool DisableManualMapControl { get; set; }
		[ObservableProperty]
		public partial bool KeepRegion { get; set; }
		[ObservableProperty]
		public partial bool AutoFocus { get; set; } = true;
		[ObservableProperty]
		public partial double MaxNavigateZoom { get; set; } = 8.5;
		[ObservableProperty]
		public partial bool ShowGrid { get; set; } = false;

		[ObservableProperty]
		public partial Location Location1 { get; set; } = new(45.619358f, 145.77399f);
		[ObservableProperty]
		public partial Location Location2 { get; set; } = new(29.997368f, 128.22534f);

		[ObservableProperty]
		public partial bool AutoFocusAnimation { get; set; } = true;

		[ObservableProperty]
		public partial bool UseMiniMap { get; set; } = true;

		/// <summary>
		/// 慣性スクロールを有効にするかどうか
		/// </summary>
		[ObservableProperty]
		public partial bool IsInertiaEnabled { get; set; } = true;
	}

	[ObservableProperty]
	public partial DmdataConfig Dmdata { get; set; } = new();
	public partial class DmdataConfig : ObservableObject
	{
		public const string DefaultOAuthClientId = "CId._xg46xWbfdrOqxN7WtwNfBUL3fhKLH9roksSfV8RV3Nj";
		[ObservableProperty]
		public partial string OAuthClientId { get; set; } = DefaultOAuthClientId;
		[ObservableProperty]
		public partial string? OAuthClientSecret { get; set; }
		[ObservableProperty]
		public partial string? RefreshToken { get; set; }
		[ObservableProperty]
		public partial bool ReceiveTraining { get; set; }
		[ObservableProperty]
		public partial bool UseWebSocket { get; set; } = true;
		[ObservableProperty]
		public partial float PullMultiply { get; set; } = 1;

		[ObservableProperty]
		public partial bool UseRedundancy { get; set; } = false;

		// APIベースURL（UIから変更不可）
		[ObservableProperty]
		public partial string? ApiBaseUrl { get; set; }

		// データAPIベースURL（UIから変更不可）
		[ObservableProperty]
		public partial string? DataApiBaseUrl { get; set; }

		// WebSocketデフォルトエンドポイント（UIから変更不可）
		[ObservableProperty]
		public partial string? WebSocketDefaultEndpoint { get; set; }

		// WebSocket冗長性エンドポイント（UIから変更不可）
		[ObservableProperty]
		public partial string[]? WebSocketRedundantEndpoints { get; set; }
	}

	[ObservableProperty]
	public partial EarthquakeConfig Earthquake { get; set; } = new();
	public partial class EarthquakeConfig : ObservableObject
	{
		[ObservableProperty]
		public partial bool FillSokuhou { get; set; } = true;
		[ObservableProperty]
		public partial bool FillDetail { get; set; } = false;

		[ObservableProperty]
		public partial bool ShowHistory { get; set; } = true;

		[ObservableProperty]
		public partial bool SwitchAtUpdate { get; set; }

		[ObservableProperty]
		public partial bool ShowIntensityLegend { get; set; } = true;
	}

	[ObservableProperty]
	public partial TsunamiConfig Tsunami { get; set; } = new();
	public partial class TsunamiConfig : ObservableObject
	{
		[ObservableProperty]
		public partial bool SwitchAtUpdate { get; set; }
	}

	[ObservableProperty]
	public partial RadarConfig Radar { get; set; } = new();
	public partial class RadarConfig : ObservableObject
	{
		[ObservableProperty]
		public partial bool AutoUpdate { get; set; } = true;
	}

	[ObservableProperty]
	public partial RawIntensityObjectConfig RawIntensityObject { get; set; } = new();
	public partial class RawIntensityObjectConfig : ObservableObject
	{
		[ObservableProperty]
		public partial double ShowNameZoomLevel { get; set; } = 9;

		[ObservableProperty]
		public partial double MinShownIntensity { get; set; } = -3;

		[ObservableProperty]
		public partial double MinShownDetailIntensity { get; set; } = -3;

		[ObservableProperty]
		public partial bool ShowInvalidateIcon { get; set; } = true;
	}

	[ObservableProperty]
	public partial AudioConfig Audio { get; set; } = new();
	public partial class AudioConfig : ObservableObject
	{
		[ObservableProperty]
		public partial double GlobalVolume { get; set; } = 1;

		[ObservableProperty]
		public partial bool IsMuted { get; set; } = false;

		[ObservableProperty]
		public partial bool ShowMuteButtonInMainWindow { get; set; } = true;
	}

	[ObservableProperty]
	public partial Dictionary<string, Dictionary<string, SoundConfig>> Sounds { get; set; } = [];
	public partial class SoundConfig : ObservableObject
	{
		[ObservableProperty]
		public partial bool Enabled { get; set; } = false;
		[ObservableProperty]
		public partial string FilePath { get; set; } = "";
		[ObservableProperty]
		public partial double Volume { get; set; } = 1;
		[ObservableProperty]
		public partial bool AllowMultiPlay { get; set; } = false;
	}

	[ObservableProperty]
	public partial QzssConfig Qzss { get; set; } = new();
	public partial class QzssConfig : ObservableObject
	{
		[ObservableProperty]
		public partial bool Connect { get; set; } = false;

		[ObservableProperty]
		public partial string SerialPort { get; set; } = "";

		[ObservableProperty]
		public partial int BaudRate { get; set; } = 115200;

		[ObservableProperty]
		public partial bool ShowCurrentPositionInMap { get; set; } = false;

		[ObservableProperty]
		public partial bool HidePositionNumber { get; set; } = true;

		[ObservableProperty]
		public partial bool IgnoreOtherOrganizationReport { get; set; } = true;

		[ObservableProperty]
		public partial bool IgnoreTrainingOrTestReport { get; set; } = true;

		[ObservableProperty]
		public partial int TimezoneOffset { get; set; } = -9;

		// 衛星航法データ出力を有効化するメッセージを送信する
		[ObservableProperty]
		public partial bool SetupSendSfrbx { get; set; } = true;

		// NMEA RMC 出力を有効化するメッセージを送信する
		[ObservableProperty]
		public partial bool SetupSendRmc { get; set; } = false;

		// QZSS 信号の受信を有効化するメッセージを送信する
		[ObservableProperty]
		public partial bool SetupEnableQzss { get; set; } = false;

		// 更新レート(計測間隔)を変更する
		[ObservableProperty]
		public partial bool SetupChangeUpdateRate { get; set; } = true;

		// 更新レート(ms)
		[ObservableProperty]
		public partial int SetupUpdateRateMs { get; set; } = 200;

		// ボーレートを変更する
		[ObservableProperty]
		public partial bool SetupChangeBaudRate { get; set; } = true;

		// 設定送信時のボーレート
		[ObservableProperty]
		public partial int SetupBaudRate { get; set; } = 115200;
	}

	[ObservableProperty]
	public partial VoicevoxConfig Voicevox { get; set; } = new();
	public partial class VoicevoxConfig : ObservableObject
	{
		[ObservableProperty]
		public partial bool Enabled { get; set; } = false;

		[ObservableProperty]
		public partial string Address { get; set; } = "http://localhost:50021/";

		[ObservableProperty]
		public partial int SpeakerId { get; set; } = 2;

		[ObservableProperty]
		public partial float SpeedScale { get; set; } = 1;

		[ObservableProperty]
		public partial float PitchScale { get; set; } = 0;

		[ObservableProperty]
		public partial float IntonationScale { get; set; } = 1;

		[ObservableProperty]
		public partial float VolumeScale { get; set; } = 1;

		[ObservableProperty]
		public partial float PauseLengthScale { get; set; } = .75f;

		[ObservableProperty]
		public partial bool ClearCacheImmediately { get; set; } = false;

		[ObservableProperty]
		public partial bool EnableAutoCacheCleanup { get; set; } = true;

		[ObservableProperty]
		public partial int CacheMaxDays { get; set; } = 7;
	}

	[ObservableProperty]
	public partial AxisConfig Axis { get; set; } = new();
	public partial class AxisConfig : ObservableObject
	{
		[ObservableProperty]
		public partial bool Enable { get; set; } = false;
		[ObservableProperty]
		public partial string Jwt { get; set; } = "";
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
