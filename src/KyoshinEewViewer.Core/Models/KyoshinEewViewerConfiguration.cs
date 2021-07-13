using KyoshinMonitorLib;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Core.Models
{
	public class KyoshinEewViewerConfiguration : ReactiveObject
	{
		[Reactive]
		public double WindowScale { get; set; } = 1;

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
			public bool HideShindoIcon { get; set; }

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
		}

		[Reactive]
		public NotificationConfig Notification { get; set; } = new NotificationConfig();
		public class NotificationConfig : ReactiveObject
		{
			public NotificationConfig()
			{
				Enable = true;
				HideWhenMinimizeWindow = true;
			}
			[Reactive]
			public bool Enable { get; set; }
			[Reactive]
			public bool HideWhenMinimizeWindow { get; set; }
			[Reactive]
			public bool HideWhenClosingWindow { get; set; }
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
			[Reactive]
			public string OAuthClientId { get; set; } = "CId._xg46xWbfdrOqxN7WtwNfBUL3fhKLH9roksSfV8RV3Nj";
			[Reactive]
			public string? RefleshToken { get; set; }
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
		}

		[Reactive]
		public RawIntensityObjectConfig RawIntensityObject { get; set; } = new RawIntensityObjectConfig();
		public class RawIntensityObjectConfig : ReactiveObject
		{
			public RawIntensityObjectConfig()
			{
				ShowNameZoomLevel = 9;
				ShowValueZoomLevel = 9.5;
				MinShownIntensity = -3;
				ShowInvalidateIcon = true;
			}

			[Reactive]
			public double ShowNameZoomLevel { get; set; }
			[Reactive]
			public double ShowValueZoomLevel { get; set; }

			[Reactive]
			public double MinShownIntensity { get; set; }

			[Reactive]
			public bool ShowIntensityIcon { get; set; }

			[Reactive]
			public bool ShowInvalidateIcon { get; set; }
		}

		[Reactive]
		public LinuxConfig Linux { get; set; } = new LinuxConfig();
		public class LinuxConfig : ReactiveObject
		{
			public LinuxConfig()
			{
				UrlOpener = "xdg-open";
			}

			[Reactive]
			public string UrlOpener { get; set; }
		}
	}
}
