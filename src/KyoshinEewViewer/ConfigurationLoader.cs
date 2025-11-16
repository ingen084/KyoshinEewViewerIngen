using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Services.Workflows;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;

namespace KyoshinEewViewer;

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

	/// <summary>
	/// 設定ファイルがアプリケーションデータディレクトリから読み込まれたか
	/// </summary>
	private static bool _configLoadedFromApplicationData = true;

	/// <summary>
	/// ワークフローファイルがアプリケーションデータディレクトリから読み込まれたか
	/// </summary>
	private static bool _workflowsLoadedFromApplicationData = true;

	public static KyoshinEewViewerConfiguration Load()
	{
		KyoshinEewViewerConfiguration? config;

		// 旧バージョンから設定ファイルを移行
		MigrateLegacyConfigFiles();

		try
		{
			// カレントディレクトリを優先して読み込み、次にアプリケーションデータディレクトリ
			if (LoadPrivate(out config, false))
				_configLoadedFromApplicationData = false;
			else if (LoadPrivate(out config, true))
				_configLoadedFromApplicationData = true;
			else
			{
				config = new KyoshinEewViewerConfiguration();
				_configLoadedFromApplicationData = true;
			}
		}
		catch (UnauthorizedAccessException)
		{
			if (!LoadPrivate(out config, true) || config == null)
				config = new KyoshinEewViewerConfiguration();
			_configLoadedFromApplicationData = true;
		}
		catch
		{
			config = new KyoshinEewViewerConfiguration();
			_configLoadedFromApplicationData = true;
		}

		if (System.Reflection.Assembly.GetExecutingAssembly().GetName()?.Version?.Minor != 0)
			config.Update.UseUnstableBuild = true;

		return config;
	}

	private static bool LoadPrivate(out KyoshinEewViewerConfiguration config, bool useApplicationDataDirectory)
	{
		config = new KyoshinEewViewerConfiguration();
		var fileName = useApplicationDataDirectory
			? Path.Combine(PlatformDirectories.ApplicationData, "config.json")
			: "config.json";
		if (!File.Exists(fileName))
			return false;

		var v = JsonSerializer.Deserialize<KyoshinEewViewerConfiguration>(File.ReadAllText(fileName), ConfigSerializeOption);
		if (v == null)
			return false;

		config = v;

		// 以前のバージョンからの移行処理
		MigrateSettings(config);

		return true;
	}

	/// <summary>
	/// 設定の移行処理
	/// </summary>
	private static void MigrateSettings(KyoshinEewViewerConfiguration config)
	{
		// SavedVersionがnullまたはv0.19以前の場合
		if (config.SavedVersion == null || config.SavedVersion < new Version(0, 20))
		{
			// ログ出力が有効な場合、UseCurrentDirectoryを有効化
			if (config.Logging.Enable && !config.Logging.UseCurrentDirectory)
			{
				config.Logging.UseCurrentDirectory = true;
			}
		}
	}

	/// <summary>
	/// 旧バージョンから設定ファイルを移行
	/// </summary>
	private static void MigrateLegacyConfigFiles()
	{
		try
		{
			var migratedCount = PlatformDirectories.MigrateLegacyFiles(
				["config.json", "workflows.json"],
				PlatformDirectories.ApplicationData);

			if (migratedCount > 0)
			{
				// 移行されたファイルがある場合はコンソールに出力
				Console.WriteLine($"設定ファイルを新しいディレクトリに移行しました: {migratedCount}個");
			}
		}
		catch
		{
			// 移行失敗は無視
		}
	}

	public static void Save(KyoshinEewViewerConfiguration config)
	{
		try
		{
			SavePrivate(config, _configLoadedFromApplicationData);
		}
		catch (UnauthorizedAccessException)
		{
			SavePrivate(config, !_configLoadedFromApplicationData);
		}
		catch { }
	}
	private static void SavePrivate(KyoshinEewViewerConfiguration config, bool useApplicationDataDirectory)
	{
		var fileName = useApplicationDataDirectory
			? Path.Combine(PlatformDirectories.ApplicationData, "config.json")
			: "config.json";

		if (useApplicationDataDirectory)
			PlatformDirectories.EnsureDirectoryExists(PlatformDirectories.ApplicationData);

		config.SavedVersion = System.Reflection.Assembly.GetEntryAssembly()?.GetName()?.Version;
		config.SavedVersionWithSuffix = Utils.InternalVersion;
		File.WriteAllText(fileName, JsonSerializer.Serialize(config, ConfigSerializeOption));
	}

	public static Workflow[] LoadWorkflows()
	{
		Workflow[]? config;
		try
		{
			// カレントディレクトリを優先して読み込み、次にアプリケーションデータディレクトリ
			if (LoadWorkflowsPrivate(out config, false))
				_workflowsLoadedFromApplicationData = false;
			else if (LoadWorkflowsPrivate(out config, true))
				_workflowsLoadedFromApplicationData = true;
			else
			{
				config = [];
				_workflowsLoadedFromApplicationData = true;
			}
		}
		catch (UnauthorizedAccessException)
		{
			if (!LoadWorkflowsPrivate(out config, true) || config == null)
				config = [];
			_workflowsLoadedFromApplicationData = true;
		}
		catch
		{
			config = [];
			_workflowsLoadedFromApplicationData = true;
		}

		return config;
	}

	private static bool LoadWorkflowsPrivate(out Workflow[] config, bool useApplicationDataDirectory)
	{
		config = [];
		var fileName = useApplicationDataDirectory
			? Path.Combine(PlatformDirectories.ApplicationData, "workflows.json")
			: "workflows.json";
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
			SaveWorkflowsPrivate(config, _workflowsLoadedFromApplicationData);
		}
		catch (UnauthorizedAccessException)
		{
			SaveWorkflowsPrivate(config, !_workflowsLoadedFromApplicationData);
		}
		catch
		{
		}
	}


	private static void SaveWorkflowsPrivate(Workflow[] config, bool useApplicationDataDirectory)
	{
		var fileName = useApplicationDataDirectory
			? Path.Combine(PlatformDirectories.ApplicationData, "workflows.json")
			: "workflows.json";

		if (useApplicationDataDirectory)
			PlatformDirectories.EnsureDirectoryExists(PlatformDirectories.ApplicationData);

		File.WriteAllText(fileName, JsonSerializer.Serialize(config, WorkflowSerializeOption));
	}
}
