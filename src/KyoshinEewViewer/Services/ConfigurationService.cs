using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Events;
using ReactiveUI;
using System.IO;
using System.Text.Json;

namespace KyoshinEewViewer.Services
{
	public class ConfigurationService
	{
		private const string ConfigurationFileName = "config.json";
		public KyoshinEewViewerConfiguration Configuration { get; }

		public ConfigurationService()
		{
			var config = LoadConfigure();
			if (config == null)
			{
				Configuration = new KyoshinEewViewerConfiguration();
				if (System.Reflection.Assembly.GetExecutingAssembly().GetName()?.Version?.Minor != 0)
					Configuration.Update.UseUnstableBuild = true;
				SaveConfigure(Configuration);
			}
			else
				Configuration = config;
		}

		public static KyoshinEewViewerConfiguration? LoadConfigure()
			=> !File.Exists(ConfigurationFileName)
				? null
				: JsonSerializer.Deserialize<KyoshinEewViewerConfiguration>(File.ReadAllText(ConfigurationFileName));

		public static void SaveConfigure(KyoshinEewViewerConfiguration config)
			=> File.WriteAllText(ConfigurationFileName, JsonSerializer.Serialize(config));
	}
}