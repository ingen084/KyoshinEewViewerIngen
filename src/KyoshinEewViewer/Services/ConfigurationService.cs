using Prism.Events;
using Prism.Mvvm;
using System;
using System.IO;
using System.Text.Json;

namespace KyoshinEewViewer.Services
{
	public class ConfigurationService
	{
		private const string ConfigurationFileName = "config.json";
		public Configuration Configuration { get; }

		public ConfigurationService(IEventAggregator aggregator)
		{
			if ((Configuration = LoadConfigure()) == null)
			{
				Configuration = new Configuration();
				if (System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.Minor != 0)
					Configuration.Update.UseUnstableBuild = true;
				SaveConfigure(Configuration);
			}
			aggregator.GetEvent<Events.ApplicationClosing>().Subscribe(()
				=> SaveConfigure(Configuration));
		}

		public static Configuration LoadConfigure()
			=> !File.Exists(ConfigurationFileName)
				? null
				: JsonSerializer.Deserialize<Configuration>(File.ReadAllText(ConfigurationFileName));

		public static void SaveConfigure(Configuration config)
			=> File.WriteAllText(ConfigurationFileName, JsonSerializer.Serialize(config));
	}

	public class Configuration : BindableBase
	{
		private TimerConfig timer = new TimerConfig();
		public TimerConfig Timer
		{
			get => timer;
			set => SetProperty(ref timer, value);
		}
		public class TimerConfig : BindableBase
		{
			private int offset = 1100;
			public int Offset
			{
				get => offset;
				set => SetProperty(ref offset, value);
			}
			private bool autoOffsetIncrement = true;
			public bool AutoOffsetIncrement
			{
				get => autoOffsetIncrement;
				set => SetProperty(ref autoOffsetIncrement, value);
			}
		}

		private KyoshinMonitorConfig kyoshinMonitor = new KyoshinMonitorConfig();
		public KyoshinMonitorConfig KyoshinMonitor
		{
			get => kyoshinMonitor;
			set => SetProperty(ref kyoshinMonitor, value);
		}
		public class KyoshinMonitorConfig : BindableBase
		{
			private bool useImageParse = true;
			public bool UseImageParse
			{
				get => useImageParse;
				set => SetProperty(ref useImageParse, value);
			}
			private bool alwaysImageParse;
			public bool AlwaysImageParse
			{
				get => alwaysImageParse;
				set => SetProperty(ref alwaysImageParse, value);
			}
		}

		private ThemeConfig theme = new ThemeConfig();
		public ThemeConfig Theme
		{
			get => theme;
			set => SetProperty(ref theme, value);
		}
		public class ThemeConfig : BindableBase
		{
			private string windowThemeName = "Light";
			public string WindowThemeName
			{
				get => windowThemeName;
				set => SetProperty(ref windowThemeName, value);
			}
			private string intensityThemeName = "Standard";
			public string IntensityThemeName
			{
				get => intensityThemeName;
				set => SetProperty(ref intensityThemeName, value);
			}
		}

		private NetworkTimeConfig networkTime = new NetworkTimeConfig();
		public NetworkTimeConfig NetworkTime
		{
			get => networkTime;
			set => SetProperty(ref networkTime, value);
		}
		public class NetworkTimeConfig : BindableBase
		{
			private bool enable = true;
			public bool Enable
			{
				get => enable;
				set => SetProperty(ref enable, value);
			}

			private bool useHttp = false;
			public bool UseHttp
			{
				get => useHttp;
				set => SetProperty(ref useHttp, value);
			}

			private string address = "ntp.nict.jp";
			public string Address
			{
				get => address;
				set => SetProperty(ref address, value);
			}
		}

		private LoggingConfig logging = new LoggingConfig();
		public LoggingConfig Logging
		{
			get => logging;
			set => SetProperty(ref logging, value);
		}
		public class LoggingConfig : BindableBase
		{
			private bool enable = false;
			public bool Enable
			{
				get => enable;
				set => SetProperty(ref enable, value);
			}
			private string directory = "Logs";
			public string Directory
			{
				get => directory;
				set => SetProperty(ref directory, value);
			}
		}

		private UpdateConfig update = new UpdateConfig();
		public UpdateConfig Update
		{
			get => update;
			set => SetProperty(ref update, value);
		}
		public class UpdateConfig : BindableBase
		{
			private bool enable = true;
			public bool Enable
			{
				get => enable;
				set => SetProperty(ref enable, value);
			}

			private bool useUnstableBuild;
			public bool UseUnstableBuild
			{
				get => useUnstableBuild;
				set => SetProperty(ref useUnstableBuild, value);
			}
		}

		private NotificationConfig notification = new NotificationConfig();
		public NotificationConfig Notification
		{
			get => notification;
			set => SetProperty(ref notification, value);
		}
		public class NotificationConfig : BindableBase
		{
			private bool enable = true;
			public bool Enable
			{
				get => enable;
				set => SetProperty(ref enable, value);
			}

			private bool hideWhenMinimizeWindow = true;
			public bool HideWhenMinimizeWindow
			{
				get => hideWhenMinimizeWindow;
				set => SetProperty(ref hideWhenMinimizeWindow, value);
			}

			private bool hideWhenClosingWindow;
			public bool HideWhenClosingWindow
			{
				get => hideWhenClosingWindow;
				set => SetProperty(ref hideWhenClosingWindow, value);
			}
		}
	}
}