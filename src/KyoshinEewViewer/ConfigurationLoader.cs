using KyoshinEewViewer.Core.Models;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace KyoshinEewViewer;

public static class ConfigurationLoader
{
	private static JsonSerializerOptions SerializeOption { get; } = new()
	{
		IgnoreReadOnlyFields = true,
		IgnoreReadOnlyProperties = true,
	};

	public static KyoshinEewViewerConfiguration Load()
	{
		if (!LoadPrivate(out var config, RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) && !LoadPrivate(out config, true) || config == null)
			config = new KyoshinEewViewerConfiguration();

		if (System.Reflection.Assembly.GetExecutingAssembly().GetName()?.Version?.Minor != 0)
			config.Update.UseUnstableBuild = true;

		return config;
	}
	private static bool LoadPrivate(out KyoshinEewViewerConfiguration? config, bool useHomeDirectory)
	{
		config = null;
		var fileName = useHomeDirectory ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".kevi", "config.json") : "config.json";
		if (!File.Exists(fileName))
			return false;

		var v = JsonSerializer.Deserialize<KyoshinEewViewerConfiguration>(File.ReadAllText(fileName), SerializeOption);
		if (v == null)
			return false;

		config = v;
		return true;
	}

	public static void Save(KyoshinEewViewerConfiguration config)
	{
		try
		{
			SavePrivate(config, RuntimeInformation.IsOSPlatform(OSPlatform.OSX));
		}
		catch (UnauthorizedAccessException)
		{
			SavePrivate(config, true);
		}
		catch { }
	}
	private static void SavePrivate(KyoshinEewViewerConfiguration config, bool useHomeDirectory)
	{
		if (useHomeDirectory && !Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".kevi")))
			Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".kevi"));

		var fileName = useHomeDirectory ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".kevi", "config.json") : "config.json";
		config.SavedVersion = System.Reflection.Assembly.GetEntryAssembly()?.GetName()?.Version;
		File.WriteAllText(fileName, JsonSerializer.Serialize(config, SerializeOption));
	}
}
