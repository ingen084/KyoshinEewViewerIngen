using KyoshinEewViewer.Core.Models;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace KyoshinEewViewer.Services;

public static class ConfigurationService
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

	private static JsonSerializerOptions SerializeOption { get; } = new()
	{
		IgnoreReadOnlyFields = true,
		IgnoreReadOnlyProperties = true,
	};

	public static void Load()
	{
		if (LoadPrivate(RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) ||
			LoadPrivate(true))
		{
			if (System.Reflection.Assembly.GetExecutingAssembly().GetName()?.Version?.Minor != 0)
				Current.Update.UseUnstableBuild = true;
			return;
		}

		Current = new KyoshinEewViewerConfiguration();
		if (System.Reflection.Assembly.GetExecutingAssembly().GetName()?.Version?.Minor != 0)
			Current.Update.UseUnstableBuild = true;
	}
	private static bool LoadPrivate(bool useHomeDirectory)
	{
		var fileName = useHomeDirectory ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kevi", "config.json") : "config.json";
		if (!File.Exists(fileName))
			return false;

		var v = JsonSerializer.Deserialize<KyoshinEewViewerConfiguration>(File.ReadAllText(fileName), SerializeOption);
		if (v == null)
			return false;

		Current = v;
		if (System.Reflection.Assembly.GetExecutingAssembly().GetName()?.Version?.Minor != 0)
			Current.Update.UseUnstableBuild = true;

		return true;
	}

	public static void Save()
	{
		try
		{
			SavePrivate(RuntimeInformation.IsOSPlatform(OSPlatform.OSX));
		}
		catch (UnauthorizedAccessException)
		{
			SavePrivate(true);
		}
	}
	private static void SavePrivate(bool useHomeDirectory)
	{
		if (useHomeDirectory && Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kevi")))
			Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kevi"));

		var fileName = useHomeDirectory ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kevi", "config.json") : "config.json";
		Current.SavedVersion = System.Reflection.Assembly.GetEntryAssembly()?.GetName()?.Version;
		File.WriteAllText(fileName, JsonSerializer.Serialize(Current, SerializeOption));
	}
}
