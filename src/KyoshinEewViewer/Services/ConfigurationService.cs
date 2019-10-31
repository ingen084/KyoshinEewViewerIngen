using Prism.Events;
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
					Configuration.UseUnstableBuild = true;
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

	public class Configuration
	{
		public event Action<string> ConfigurationUpdated;

		private int offset = 1100;

		public int Offset
		{
			get => offset;
			set
			{
				if (offset == value)
					return;

				offset = value;
				ConfigurationUpdated?.Invoke(nameof(Offset));
			}
		}

		public string WindowThemeId { get; set; } = "Light";
		public string IntensityThemeId { get; set; } = "Standard";

		#region インターネット時刻

		private bool enableNetworkTimeSync = true;

		public bool EnableNetworkTimeSync
		{
			get => enableNetworkTimeSync;
			set
			{
				if (enableNetworkTimeSync == value)
					return;
				enableNetworkTimeSync = value;
				ConfigurationUpdated?.Invoke(nameof(EnableNetworkTimeSync));
			}
		}

		private bool useHttpNetworkTime = false;

		public bool UseHttpNetworkTime
		{
			get => useHttpNetworkTime;
			set
			{
				if (useHttpNetworkTime == value)
					return;
				useHttpNetworkTime = value;
				ConfigurationUpdated?.Invoke(nameof(UseHttpNetworkTime));
			}
		}

		private string networkTimeSyncAddress = "ntp.nict.jp";

		public string NetworkTimeSyncAddress
		{
			get => networkTimeSyncAddress;
			set
			{
				if (networkTimeSyncAddress == value)
					return;
				networkTimeSyncAddress = value;
				ConfigurationUpdated?.Invoke(nameof(NetworkTimeSyncAddress));
			}
		}

		#endregion インターネット時刻

		#region ログ

		public bool EnableLogging { get; set; } = false;
		public string LogDirectory { get; set; } = "Logs";

		#endregion ログ

		#region 画像解析

		public bool UseImageParseMode { get; set; } = true;
		private bool alwaysUseImageParseMode = false;

		public bool AlwaysUseImageParseMode
		{
			get => alwaysUseImageParseMode;
			set
			{
				if (alwaysUseImageParseMode == value)
					return;
				alwaysUseImageParseMode = value;
				ConfigurationUpdated?.Invoke(nameof(AlwaysUseImageParseMode));
			}
		}

		#endregion 画像解析

		#region アップデート

		private bool enableAutoUpdateCheck = true;

		public bool EnableAutoUpdateCheck
		{
			get => enableAutoUpdateCheck;
			set
			{
				if (enableAutoUpdateCheck == value)
					return;
				enableAutoUpdateCheck = value;
				ConfigurationUpdated?.Invoke(nameof(EnableAutoUpdateCheck));
			}
		}

		private bool useUnstableBuild = false;

		public bool UseUnstableBuild
		{
			get => useUnstableBuild;
			set
			{
				if (useUnstableBuild == value)
					return;
				useUnstableBuild = value;
				ConfigurationUpdated?.Invoke(nameof(UseUnstableBuild));
			}
		}

		#endregion アップデート

		#region 通知領域

		public bool EnableNotifyIcon { get; set; } = true;
		public bool WindowHideWhenMinimize { get; set; } = true;
		public bool WhidowHideWhenClosing { get; set; } = false;

		#endregion 通知領域
	}
}