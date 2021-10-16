using Avalonia.Controls;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Text.Json.Serialization;
using Location = KyoshinMonitorLib.Location;

namespace KyoshinEewViewer.Core.Models
{
	public class KyoshinEewViewerConfiguration : ReactiveObject
	{
		[Reactive]
		public double WindowScale { get; set; } = 1;
		[Reactive]
		public WindowState WindowState { get; set; } = WindowState.Normal;
		[Reactive]
		public Point2D? WindowSize { get; set; }
		[Reactive]
		public Point2D? WindowLocation { get; set; }

		public record Point2D(double X, double Y);

		[Reactive]
		public Version? SavedVersion { get; set; }

		[Reactive]
		public TimerConfig Timer { get; set; } = new TimerConfig();
		public class TimerConfig : ReactiveObject
		{
			public TimerConfig()
			{
				Offset = 1100;
				AutoOffsetIncrement = true;
			}
			[Reactive]
			public int Offset { get; set; }
			[Reactive]
			public bool AutoOffsetIncrement { get; set; }

			[Reactive, JsonIgnore]
			public int TimeshiftSeconds { get; set; }
		}

		[Reactive]
		public KyoshinMonitorConfig KyoshinMonitor { get; set; } = new KyoshinMonitorConfig();
		public class KyoshinMonitorConfig : ReactiveObject
		{
			[Reactive]
			public bool Enabled { get; set; } = true;

			[Reactive]
			public string ListRenderMode { get; set; } = "ShindoIcon";

			[Reactive]
			public int FetchFrequency { get; set; } = 1;
			[Reactive]
			public bool ForcefetchOnEew { get; set; }
		}

		[Reactive]
		public EewConfig Eew { get; set; } = new EewConfig();
		public class EewConfig : ReactiveObject
		{
			[Reactive]
			public bool EnableLast10Second { get; set; }

			[Reactive]
			public bool EnableSignalNowProfessional { get; set; }
		}

		[Reactive]
		public ThemeConfig Theme { get; set; } = new ThemeConfig();
		public class ThemeConfig : ReactiveObject
		{
			[Reactive]
			public string WindowThemeName { get; set; } = "Light";
			[Reactive]
			public string IntensityThemeName { get; set; } = "Standard";
		}

		[Reactive]
		public NetworkTimeConfig NetworkTime { get; set; } = new NetworkTimeConfig();
		public class NetworkTimeConfig : ReactiveObject
		{
			[Reactive]
			public bool Enable { get; set; } = true;
			[Reactive]
			public string Address { get; set; } = "ntp.nict.jp";
		}

		[Reactive]
		public LoggingConfig Logging { get; set; } = new LoggingConfig();
		public class LoggingConfig : ReactiveObject
		{
			[Reactive]
			public bool Enable { get; set; } = false;
			[Reactive]
			public string Directory { get; set; } = "Logs";
		}

		[Reactive]
		public UpdateConfig Update { get; set; } = new UpdateConfig();
		public class UpdateConfig : ReactiveObject
		{
			[Reactive]
			public bool Enable { get; set; } = true;
			[Reactive]
			public bool UseUnstableBuild { get; set; }
			[Reactive]
			public bool SendCrashReport { get; set; } = true;
		}

		[Reactive]
		public NotificationConfig Notification { get; set; } = new NotificationConfig();
		public class NotificationConfig : ReactiveObject
		{
			[Reactive]
			public bool TrayIconEnable { get; set; } = true;
			[Reactive]
			public bool HideWhenMinimizeWindow { get; set; } = true;
			[Reactive]
			public bool HideWhenClosingWindow { get; set; }

			[Reactive]
			public bool Enable { get; set; } = true;
			[Reactive]
			public bool SwitchEqSource { get; set; } = true;
			[Reactive]
			public bool GotEq { get; set; } = true;
			[Reactive]
			public bool EewReceived { get; set; } = true;
		}

		[Reactive]
		public MapConfig Map { get; set; } = new MapConfig();
		public class MapConfig : ReactiveObject
		{
			[Reactive]
			public bool DisableManualMapControl { get; set; }
			[Reactive]
			public bool KeepRegion { get; set; }
			[Reactive]
			public bool AutoFocus { get; set; } = true;

			[Reactive]
			public double MaxNavigateZoom { get; set; } = 8.5;
			[Reactive]
			public bool ShowGrid { get; set; } = false;

			[Reactive]
			public Location Location1 { get; set; } = new Location(24.058240f, 123.046875f);
			[Reactive]
			public Location Location2 { get; set; } = new Location(45.706479f, 146.293945f);

			[Reactive]
			public bool AutoFocusAnimation { get; set; } = true;
		}

		[Reactive]
		public DmdataConfig Dmdata { get; set; } = new DmdataConfig();
		public class DmdataConfig : ReactiveObject
		{
			public const string DefaultOAuthClientId = "CId._xg46xWbfdrOqxN7WtwNfBUL3fhKLH9roksSfV8RV3Nj";
			[Reactive]
			public string OAuthClientId { get; set; } = DefaultOAuthClientId;
			[Reactive]
			public string? RefreshToken { get; set; }
			[Reactive]
			public bool UseWebSocket { get; set; } = true;
			[Reactive]
			public float PullMultiply { get; set; } = 1;
		}

		[Reactive]
		public EarthquakeConfig Earthquake { get; set; } = new EarthquakeConfig();
		public class EarthquakeConfig : ReactiveObject
		{
			[Reactive]
			public bool Enabled { get; set; } = true;

			[Reactive]
			public bool FillSokuhou { get; set; } = true;
			[Reactive]
			public bool FillDetail { get; set; } = false;
		}

		[Reactive]
		public RadarConfig Radar { get; set; } = new();
		public class RadarConfig : ReactiveObject
		{
			[Reactive]
			public bool Enabled { get; set; } = false;
			[Reactive]
			public bool AutoUpdate { get; set; } = true;
		}

		[Reactive]
		public RawIntensityObjectConfig RawIntensityObject { get; set; } = new RawIntensityObjectConfig();
		public class RawIntensityObjectConfig : ReactiveObject
		{
			[Reactive]
			public double ShowNameZoomLevel { get; set; } = 9;
			[Reactive]
			public double ShowValueZoomLevel { get; set; } = 9.5;

			[Reactive]
			public double MinShownIntensity { get; set; } = -3;

			[Reactive]
			public bool ShowIntensityIcon { get; set; }

			[Reactive]
			public bool ShowInvalidateIcon { get; set; } = true;
		}

		[Reactive]
		public LinuxConfig Linux { get; set; } = new LinuxConfig();
		public class LinuxConfig : ReactiveObject
		{
			[Reactive]
			public string UrlOpener { get; set; } = "xdg-open";
		}

		[Reactive]
		public WindowsConfig Windows { get; set; } = new WindowsConfig();
		public class WindowsConfig : ReactiveObject
		{
		}
	}
}
