using KyoshinEewViewer.Core.Models;
using System.IO;
using System.Text.Json;

#pragma warning disable CS8618, CS8601
namespace KyoshinEewViewer.Services
{
	public class ConfigurationService
	{
		public static KyoshinEewViewerConfiguration Default { get; private set; }

		public static void Load(string fileName = "config.json")
		{
			if (File.Exists(fileName))
			{
				Default = JsonSerializer.Deserialize<KyoshinEewViewerConfiguration>(File.ReadAllText(fileName));
				if (Default != null)
					return;
			}

			Default = new KyoshinEewViewerConfiguration();
			if (System.Reflection.Assembly.GetExecutingAssembly().GetName()?.Version?.Minor != 0)
				Default.Update.UseUnstableBuild = true;
			Save(fileName);
		}

		public static void Save(string fileName = "config.json")
			=> File.WriteAllText(fileName, JsonSerializer.Serialize(Default));
	}
}