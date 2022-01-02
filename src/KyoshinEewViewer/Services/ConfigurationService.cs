using KyoshinEewViewer.Core.Models;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace KyoshinEewViewer.Services;

public class ConfigurationService
{
	private static KyoshinEewViewerConfiguration? _current;
	public static KyoshinEewViewerConfiguration Current
	{
		get {
			if (_current == null)
				Load();
			// Load で必ずnull以外になる
			return _current!;
		}
		private set => _current = value;
	}

	public static void Load()
	{
		var fileName = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "~/.kevi/config.json" : "config.json";
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

	public static void Save()
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && !Directory.Exists("~/.kevi"))
			Directory.CreateDirectory("~/.kevi");
		var fileName = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "~/.kevi/config.json" : "config.json";
		Current.SavedVersion = System.Reflection.Assembly.GetEntryAssembly()?.GetName()?.Version;
		File.WriteAllText(fileName, JsonSerializer.Serialize(Current));
	}
}
