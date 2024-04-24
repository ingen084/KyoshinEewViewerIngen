using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Services.Workflows;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace KyoshinEewViewer;

[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "<Pending>")]
[UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "<Pending>")]
public static class ConfigurationLoader
{
	private static JsonSerializerOptions ConfigSerializeOption { get; } = new()
	{
		TypeInfoResolver = KyoshinEewViewerSerializerContext.Default,
		Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
	};

	private static JsonSerializerOptions WorkflowSerializeOption { get; } = new()
	{
		Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
		WriteIndented = true,
		IgnoreReadOnlyFields = true,
		IgnoreReadOnlyProperties = true,
	};

	public static KyoshinEewViewerConfiguration Load()
	{
		KyoshinEewViewerConfiguration? config;
		try
		{
			if (!LoadPrivate(out config, RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) && !LoadPrivate(out config, true) || config == null)
				config = new KyoshinEewViewerConfiguration();
		}
		catch (UnauthorizedAccessException)
		{
			if (!LoadPrivate(out config, true) || config == null)
				config = new KyoshinEewViewerConfiguration();
		}
		catch
		{
			config = new KyoshinEewViewerConfiguration();
		}

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

		var v = JsonSerializer.Deserialize<KyoshinEewViewerConfiguration>(File.ReadAllText(fileName), ConfigSerializeOption);
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
		File.WriteAllText(fileName, JsonSerializer.Serialize(config, ConfigSerializeOption));
	}

	public static Workflow[] LoadWorkflows()
	{
		Workflow[]? config;
		try
		{
			if (!LoadWorkflowsPrivate(out config, RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) && !LoadWorkflowsPrivate(out config, true) || config == null)
				config = Array.Empty<Workflow>();
		}
		catch (UnauthorizedAccessException)
		{
			if (!LoadWorkflowsPrivate(out config, true) || config == null)
				config = Array.Empty<Workflow>();
		}
		catch
		{
			config = Array.Empty<Workflow>();
		}

		return config;
	}

	private static bool LoadWorkflowsPrivate(out Workflow[]? config, bool useHomeDirectory)
	{
		config = null;
		var fileName = useHomeDirectory ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".kevi", "workflows.json") : "workflows.json";
		if (!File.Exists(fileName))
			return false;

		var v = JsonSerializer.Deserialize<Workflow[]>(File.ReadAllText(fileName), WorkflowSerializeOption);
		if (v == null)
			return false;

		config = v;
		return true;
	}

	public static void SaveWorkflows(Workflow[] config)
	{
		try
		{
			SaveWorkflowsPrivate(config, RuntimeInformation.IsOSPlatform(OSPlatform.OSX));
		}
		catch (UnauthorizedAccessException)
		{
			SaveWorkflowsPrivate(config, true);
		}
		catch(Exception ex)
		{
			;
		}
	}


	private static void SaveWorkflowsPrivate(Workflow[] config, bool useHomeDirectory)
	{
		if (useHomeDirectory && !Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".kevi")))
			Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".kevi"));

		var fileName = useHomeDirectory ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".kevi", "workflows.json") : "workflows.json";
		File.WriteAllText(fileName, JsonSerializer.Serialize(config, WorkflowSerializeOption));
	}
}
