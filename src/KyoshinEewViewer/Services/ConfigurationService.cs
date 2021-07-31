using KyoshinEewViewer.Core.Models;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace KyoshinEewViewer.Services
{
	public class ConfigurationService
	{
		private static KyoshinEewViewerConfiguration? _default;
		public static KyoshinEewViewerConfiguration Default
		{
			get
			{
				if (_default == null)
					Load();
#pragma warning disable CS8603 // Null 参照戻り値である可能性があります。
				return _default;
#pragma warning restore CS8603 // Null 参照戻り値である可能性があります。
			}
			private set => _default = value;
		}

		public static void Load(string fileName = "config.json")
		{
			if (File.Exists(fileName))
			{
				var v = JsonSerializer.Deserialize<KyoshinEewViewerConfiguration>(File.ReadAllText(fileName));
				if (v != null)
				{
					Default = v;
					return;
				}
			}

			Default = new KyoshinEewViewerConfiguration();
			if (System.Reflection.Assembly.GetExecutingAssembly().GetName()?.Version?.Minor != 0)
				Default.Update.UseUnstableBuild = true;
		}

		public static void Save(string fileName = "config.json")
			=> File.WriteAllText(fileName, JsonSerializer.Serialize(Default));
	}
}