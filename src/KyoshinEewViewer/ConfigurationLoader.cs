using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Services.Workflows;
using System;
using System.IO;
using System.Text.Json;

namespace KyoshinEewViewer;

public static class ConfigurationLoader
{
	private static JsonSerializerOptions ConfigSerializeOption { get; } = new()
	{
		Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
	};

	private static JsonSerializerOptions WorkflowSerializeOption { get; } = new()
	{
		Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
		WriteIndented = true,
		IgnoreReadOnlyFields = true,
		IgnoreReadOnlyProperties = true,
		// 保険: Avalonia の Control 型プロパティ (各 Trigger/Action の DisplayControl) を型グラフから除外する。
		// 各派生型には [JsonIgnore] を付与済みで WorkflowDisplayControlAnalyzer (KEVI001) が付け忘れを
		// コンパイルエラーで防ぐが、万一すり抜けても、トリミングで Control の依存型 (Avalonia.Input.Cursor)
		// のコンストラクタ引数名が ILLink で削除され NotSupportedException でワークフロー読み込みが
		// 全滅するのを防ぐための実行時の最終防御。
		TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver
		{
			Modifiers =
			{
				static typeInfo =>
				{
					for (var i = typeInfo.Properties.Count - 1; i >= 0; i--)
						if (typeof(Avalonia.Controls.Control).IsAssignableFrom(typeInfo.Properties[i].PropertyType))
							typeInfo.Properties.RemoveAt(i);
				},
			},
		},
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

		// 前回の保存中にクラッシュした際の残骸を掃除
		CleanupTempFile(fileName);

		// 本体 → .bak の順で読み込みを試す
		if (!TryDeserializeJson<KyoshinEewViewerConfiguration>(fileName, ConfigSerializeOption, out var v) &&
			!TryDeserializeJson<KyoshinEewViewerConfiguration>(fileName + ".bak", ConfigSerializeOption, out v))
			return false;

		config = v!;

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
		AtomicWriteAllText(fileName, JsonSerializer.Serialize(config, ConfigSerializeOption));
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

		// 前回の保存中にクラッシュした際の残骸を掃除
		CleanupTempFile(fileName);

		// 本体 → .bak の順で読み込みを試す
		if (!TryDeserializeJson<Workflow[]>(fileName, WorkflowSerializeOption, out var v) &&
			!TryDeserializeJson<Workflow[]>(fileName + ".bak", WorkflowSerializeOption, out v))
			return false;

		config = v!;
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

		AtomicWriteAllText(fileName, JsonSerializer.Serialize(config, WorkflowSerializeOption));
	}

	/// <summary>
	/// アトミックにファイルへ書き込む。
	/// .tmp に書き出して物理ディスクまで flush した上で、本体をリネームで置換する。
	/// 既存ファイルがあった場合は .bak としてバックアップが残る。
	/// </summary>
	private static void AtomicWriteAllText(string fileName, string content)
	{
		var tmpFileName = fileName + ".tmp";
		var bakFileName = fileName + ".bak";

		// .tmp に書き込み、OS バッファを物理ディスクへ確実に書き出す
		using (var fs = new FileStream(tmpFileName, FileMode.Create, FileAccess.Write, FileShare.None))
		{
			var bytes = System.Text.Encoding.UTF8.GetBytes(content);
			fs.Write(bytes, 0, bytes.Length);
			fs.Flush(true);
		}

		// 既存ファイルがあればアトミックに差し替え (.bak が自動で更新される)
		if (File.Exists(fileName))
			File.Replace(tmpFileName, fileName, bakFileName);
		else
			File.Move(tmpFileName, fileName);
	}

	/// <summary>
	/// 前回の保存中にクラッシュして残った .tmp ファイルを削除する
	/// </summary>
	private static void CleanupTempFile(string fileName)
	{
		var tmpFileName = fileName + ".tmp";
		if (!File.Exists(tmpFileName))
			return;
		try { File.Delete(tmpFileName); }
		catch { }
	}

	/// <summary>
	/// JSON ファイルを読み込んでデシリアライズする。ファイルが存在しない・空・破損している場合は false を返す。
	/// </summary>
	private static bool TryDeserializeJson<T>(string path, JsonSerializerOptions options, out T? value) where T : class
	{
		value = null;
		if (!File.Exists(path))
			return false;
		try
		{
			var text = File.ReadAllText(path);
			if (string.IsNullOrWhiteSpace(text))
				return false;
			value = JsonSerializer.Deserialize<T>(text, options);
			return value != null;
		}
		catch
		{
			return false;
		}
	}
}
