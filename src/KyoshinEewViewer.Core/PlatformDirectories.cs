using System;
using System.IO;
using System.Runtime.InteropServices;

namespace KyoshinEewViewer.Core;

/// <summary>
/// プラットフォーム別のアプリケーションディレクトリパスを提供するクラス
/// </summary>
public static class PlatformDirectories
{
	/// <summary>
	/// アプリケーション名
	/// </summary>
	private const string ApplicationName = "KyoshinEewViewer";

	/// <summary>
	/// 旧バージョンのディレクトリ名
	/// </summary>
	private static readonly string[] LegacyDirectoryNames = [".kevi"];

	/// <summary>
	/// アプリケーションデータディレクトリのパスを取得します
	/// </summary>
	/// <remarks>
	/// Windows: %AppData%\KyoshinEewViewer\
	/// Linux: ~/.config/KyoshinEewViewer/
	/// macOS: ~/Library/Application Support/KyoshinEewViewer/
	/// </remarks>
	public static string ApplicationData
	{
		get
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				return Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
					"Library",
					"Application Support",
					ApplicationName);
			}

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				return Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
					".config",
					ApplicationName);
			}

			// Windows
			return Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
				ApplicationName);
		}
	}

	/// <summary>
	/// ログディレクトリのパスを取得します
	/// </summary>
	/// <remarks>
	/// Windows: %AppData%\KyoshinEewViewer\
	/// Linux: ~/.config/KyoshinEewViewer/
	/// macOS: ~/Library/Logs/KyoshinEewViewer/
	/// </remarks>
	public static string Logs
	{
		get
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				return Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
					"Library",
					"Logs",
					ApplicationName);
			}

			// Windows/Linux はアプリケーションデータディレクトリと同じ
			return ApplicationData;
		}
	}

	/// <summary>
	/// ログディレクトリのパスをカスタマイズ可能かどうかを取得します
	/// </summary>
	/// <remarks>
	/// macOS では標準的なログディレクトリを使用するため、カスタマイズ不可
	/// </remarks>
	public static bool IsLogDirectoryCustomizable => !RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

	/// <summary>
	/// 指定されたディレクトリが存在しない場合は作成します
	/// </summary>
	/// <param name="path">ディレクトリパス</param>
	public static void EnsureDirectoryExists(string path)
	{
		if (!Directory.Exists(path))
			Directory.CreateDirectory(path);
	}

	/// <summary>
	/// 旧バージョンのディレクトリパスを取得します
	/// </summary>
	/// <returns>存在する旧ディレクトリのパスのリスト</returns>
	private static string[] GetLegacyDirectoryPaths()
	{
		var paths = new System.Collections.Generic.List<string>();
		var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

		foreach (var legacyName in LegacyDirectoryNames)
		{
			// ~/.kevi または ~/.config/.kevi
			var userProfilePath = Path.Combine(userProfile, legacyName);
			if (Directory.Exists(userProfilePath))
				paths.Add(userProfilePath);

			// %AppData%\.kevi (Windows) または ~/.config/.kevi (Linux/macOS)
			if (!string.Equals(userProfile, appData, StringComparison.OrdinalIgnoreCase))
			{
				var appDataPath = Path.Combine(appData, legacyName);
				if (Directory.Exists(appDataPath))
					paths.Add(appDataPath);
			}
		}

		return [.. paths];
	}

	/// <summary>
	/// 旧バージョンからファイルを移行します
	/// </summary>
	/// <param name="filesToMigrate">移行するファイル名のリスト</param>
	/// <param name="targetDirectory">移行先ディレクトリ</param>
	/// <returns>移行されたファイル数</returns>
	public static int MigrateLegacyFiles(string[] filesToMigrate, string targetDirectory)
	{
		var migratedCount = 0;
		var legacyPaths = GetLegacyDirectoryPaths();

		// ターゲットディレクトリが存在しない場合は作成
		EnsureDirectoryExists(targetDirectory);

		// 旧ディレクトリからの移行
		foreach (var legacyPath in legacyPaths)
		{
			foreach (var fileName in filesToMigrate)
			{
				var sourcePath = Path.Combine(legacyPath, fileName);
				var targetPath = Path.Combine(targetDirectory, fileName);

				// ターゲットに既に存在する場合はスキップ
				if (File.Exists(targetPath))
					continue;

				if (File.Exists(sourcePath))
				{
					try
					{
						File.Copy(sourcePath, targetPath, overwrite: false);
						migratedCount++;
					}
					catch
					{
						// 移行失敗は無視
					}
				}
			}
		}

		return migratedCount;
	}

	/// <summary>
	/// 旧バージョンのディレクトリから指定された名前のサブディレクトリ配下のファイルを移行します
	/// </summary>
	/// <param name="subDirectoryName">サブディレクトリ名</param>
	/// <param name="targetDirectory">移行先ディレクトリ</param>
	/// <returns>移行されたファイル数</returns>
	public static int MigrateLegacySubDirectory(string subDirectoryName, string targetDirectory)
	{
		var migratedCount = 0;
		var legacyPaths = GetLegacyDirectoryPaths();

		// ターゲットディレクトリが存在しない場合は作成
		EnsureDirectoryExists(targetDirectory);

		// 旧ディレクトリからの移行
		foreach (var legacyPath in legacyPaths)
		{
			var legacySubDir = Path.Combine(legacyPath, subDirectoryName);
			if (Directory.Exists(legacySubDir))
			{
				migratedCount += MigrateDirectory(legacySubDir, targetDirectory);
			}
		}

		return migratedCount;
	}

	/// <summary>
	/// ディレクトリ内のすべてのファイルを再帰的に移行します
	/// </summary>
	private static int MigrateDirectory(string sourceDirectory, string targetDirectory)
	{
		var migratedCount = 0;

		try
		{
			// ディレクトリ内のすべてのファイルを移行
			foreach (var sourceFilePath in Directory.GetFiles(sourceDirectory))
			{
				var fileName = Path.GetFileName(sourceFilePath);
				var targetFilePath = Path.Combine(targetDirectory, fileName);

				// ターゲットに既に存在する場合はスキップ
				if (File.Exists(targetFilePath))
					continue;

				try
				{
					EnsureDirectoryExists(targetDirectory);
					File.Copy(sourceFilePath, targetFilePath, overwrite: false);
					migratedCount++;
				}
				catch
				{
					// 移行失敗は無視
				}
			}

			// サブディレクトリを再帰的に処理
			foreach (var sourceSubDirectory in Directory.GetDirectories(sourceDirectory))
			{
				var subDirectoryName = Path.GetFileName(sourceSubDirectory);
				var targetSubDirectory = Path.Combine(targetDirectory, subDirectoryName);
				migratedCount += MigrateDirectory(sourceSubDirectory, targetSubDirectory);
			}
		}
		catch
		{
			// ディレクトリアクセスエラーは無視
		}

		return migratedCount;
	}
}
