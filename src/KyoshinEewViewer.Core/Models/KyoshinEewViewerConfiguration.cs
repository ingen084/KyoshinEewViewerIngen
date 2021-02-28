using KyoshinMonitorLib;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Core.Models
{
	public class KyoshinEewViewerConfiguration : ReactiveObject
	{
		public KyoshinEewViewerConfiguration()
		{
			WindowScale = 1;
			Timer = new TimerConfig();
			KyoshinMonitor = new KyoshinMonitorConfig();
			Eew = new EewConfig();
			Theme = new ThemeConfig();
			NetworkTime = new NetworkTimeConfig();
			Logging = new LoggingConfig();
			Update = new UpdateConfig();
			Notification = new NotificationConfig();
			Map = new MapConfig();
			Dmdata = new DmdataConfig();
			RawIntensityObject = new RawIntensityObjectConfig();
		}

		[Reactive]
		public double WindowScale { get; set; }

		[Reactive]
		public TimerConfig Timer { get; set; }
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
		public KyoshinMonitorConfig KyoshinMonitor { get; set; }
		public class KyoshinMonitorConfig : ReactiveObject
		{
			public KyoshinMonitorConfig()
			{
				FetchFrequency = 1;
			}

			[Reactive]
			public bool HideShindoIcon { get; set; }

			[Reactive]
			public int FetchFrequency { get; set; }
			[Reactive]
			public bool ForcefetchOnEew { get; set; }
		}

		[Reactive]
		public EewConfig Eew { get; set; }
		public class EewConfig : ReactiveObject
		{
			[Reactive]
			public bool EnableLast10Second { get; set; }

			[Reactive]
			public bool EnableSignalNowProfessional { get; set; }
		}

		[Reactive]
		public ThemeConfig Theme { get; set; }
		public class ThemeConfig : ReactiveObject
		{
			public ThemeConfig()
			{
				WindowThemeName = "Light";
				IntensityThemeName = "Standard";
			}

			[Reactive]
			public string WindowThemeName { get; set; }
			[Reactive]
			public string IntensityThemeName { get; set; }
		}

		[Reactive]
		public NetworkTimeConfig NetworkTime { get; set; }
		public class NetworkTimeConfig : ReactiveObject
		{
			public NetworkTimeConfig()
			{
				Enable = true;
				Address = "ntp.nict.jp";
			}

			[Reactive]
			public bool Enable { get; set; }
			[Reactive]
			public string Address { get; set; }
		}

		[Reactive]
		public LoggingConfig Logging { get; set; }
		public class LoggingConfig : ReactiveObject
		{
			public LoggingConfig()
			{
				Enable = false;
				Directory = "Logs";
			}

			[Reactive]
			public bool Enable { get; set; }
			[Reactive]
			public string Directory { get; set; }
		}

		[Reactive]
		public UpdateConfig Update { get; set; }
		public class UpdateConfig : ReactiveObject
		{
			public UpdateConfig()
			{
				Enable = true;
			}
			[Reactive]
			public bool Enable { get; set; }
			[Reactive]
			public bool UseUnstableBuild { get; set; }
		}

		[Reactive]
		public NotificationConfig Notification { get; set; }
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
		public MapConfig Map { get; set; }
		public class MapConfig : ReactiveObject
		{
			public MapConfig()
			{
				Location1 = new Location(24.058240f, 123.046875f);
				Location2 = new Location(45.706479f, 146.293945f);
				AutoFocusAnimation = true;
			}
			[Reactive]
			public bool DisableManualMapControl { get; set; }
			[Reactive]
			public bool KeepRegion { get; set; }
			[Reactive]
			public bool AutoFocus { get; set; }

			[Reactive]
			public Location Location1 { get; set; }
			[Reactive]
			public Location Location2 { get; set; }

			[Reactive]
			public bool AutoFocusAnimation { get; set; }
		}

		[Reactive]
		public DmdataConfig Dmdata { get; set; }
		public class DmdataConfig : ReactiveObject
		{
			public DmdataConfig()
			{
				ApiKey = "";
				UseWebSocket = true;
				PullInterval = 1;
			}

			[Reactive]
			public string ApiKey { get; set; }
			[Reactive]
			public bool UseWebSocket { get; set; }
			[Reactive]
			public int PullInterval { get; set; }
		}

		[Reactive]
		public RawIntensityObjectConfig RawIntensityObject { get; set; }
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
	}
}
