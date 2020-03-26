using KyoshinEewViewer.Models;
using Prism.Events;
using System.IO;
using System.Text.Json;

namespace KyoshinEewViewer.Services
{
	public class ConfigurationService
	{
		private const string ConfigurationFileName = "config.json";
		public KyoshinEewViewerConfiguration Configuration { get; }

		public ConfigurationService(IEventAggregator aggregator)
		{
			if ((Configuration = LoadConfigure()) == null)
			{
				Configuration = new KyoshinEewViewerConfiguration();
				if (System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.Minor != 0)
					Configuration.Update.UseUnstableBuild = true;
				SaveConfigure(Configuration);
			}
			aggregator.GetEvent<Events.ApplicationClosing>().Subscribe(()
				=> SaveConfigure(Configuration));
		}

		public static KyoshinEewViewerConfiguration LoadConfigure()
			=> !File.Exists(ConfigurationFileName)
				? null
				: JsonSerializer.Deserialize<KyoshinEewViewerConfiguration>(File.ReadAllText(ConfigurationFileName));

		public static void SaveConfigure(KyoshinEewViewerConfiguration config)
			=> File.WriteAllText(ConfigurationFileName, JsonSerializer.Serialize(config));
	}
}