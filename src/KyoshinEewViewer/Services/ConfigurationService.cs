using KyoshinEewViewer.Core.Models;
using System.IO;
using System.Text.Json;

namespace KyoshinEewViewer.Services
{
	public class ConfigurationService
	{
		private static KyoshinEewViewerConfiguration? _current;
		public static KyoshinEewViewerConfiguration Current
		{
			get {
				if (_current == null)
					Load();
#pragma warning disable CS8603 // Null 参照戻り値である可能性があります。
				return _current;
#pragma warning restore CS8603 // Null 参照戻り値である可能性があります。
			}
			private set => _current = value;
		}

		public static void Load(string fileName = "config.json")
		{
			if (File.Exists(fileName))
			{
				var v = JsonSerializer.Deserialize<KyoshinEewViewerConfiguration>(File.ReadAllText(fileName));
				if (v != null)
				{
					Current = v;
					if (System.Reflection.Assembly.GetExecutingAssembly().GetName()?.Version?.Minor != 0)
						Current.Update.UseUnstableBuild = true;
					return;
				}
			}

			Current = new KyoshinEewViewerConfiguration();
			if (System.Reflection.Assembly.GetExecutingAssembly().GetName()?.Version?.Minor != 0)
				Current.Update.UseUnstableBuild = true;
		}

		public static void Save(string fileName = "config.json")
		{
			Current.SavedVersion = System.Reflection.Assembly.GetEntryAssembly()?.GetName()?.Version;
			File.WriteAllText(fileName, JsonSerializer.Serialize(Current));
		}
	}
}