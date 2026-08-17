using Avalonia.Platform;
using KyoshinEewViewer.Notification;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using KyoshinEewViewer.Core;

namespace KyoshinEewViewer.Desktop.Notification.Linux;

/// <summary>
/// Linux (XDG) 向けにデスクトップエントリとアイコンを生成する。
/// KDE Plasma などで通知のアプリ名・アイコン・個別通知設定を解決させるために必要。
/// </summary>
public static class LinuxDesktopEntry
{
	/// <summary>
	/// 再生成判定に使うデスクトップエントリの世代マーカー。アプリバージョンが変われば書き換える
	/// </summary>
	private static string GeneratedMarker =>
		Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
		?? "0";

	/// <summary>
	/// デスクトップエントリとアイコンを設置する (ベストエフォート、失敗しても起動は妨げない)。
	/// </summary>
	public static void TryInstall()
	{
		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			return;

		try
		{
			// AppImage / Flatpak は自前で .desktop を管理するため何もしない
			if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPIMAGE")) || File.Exists("/.flatpak-info"))
				return;

			var dataHome = GetXdgDataHome();
			// ベースディレクトリ (~/.local/share 等) が無ければ作らない (ゴミ生成防止)
			if (dataHome is null || !Directory.Exists(dataHome))
				return;

			var exePath = Environment.ProcessPath;
			if (string.IsNullOrEmpty(exePath))
				return;

			InstallIcon(dataHome);
			InstallDesktopFile(dataHome, exePath);
		}
		catch (Exception ex)
		{
			AppLog.Default.LogWarning(ex, "デスクトップエントリの生成に失敗しました");
		}
	}

	private static string? GetXdgDataHome()
	{
		var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
		if (!string.IsNullOrEmpty(xdg))
			return xdg;

		var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		return string.IsNullOrEmpty(home) ? null : Path.Combine(home, ".local", "share");
	}

	private static void InstallDesktopFile(string dataHome, string exePath)
	{
		var appsDir = Path.Combine(dataHome, "applications");
		var path = Path.Combine(appsDir, NotificationProvider.ApplicationId + ".desktop");
		var marker = GeneratedMarker;

		// 既存の Exec が現在のバイナリを参照している場合、起動オプションや環境変数によるカスタマイズとして扱う
		string? customExec = null;
		if (File.Exists(path))
		{
			var lines = File.ReadAllLines(path);
			var execValue = lines.FirstOrDefault(l => l.StartsWith("Exec=", StringComparison.Ordinal))?["Exec=".Length..].Trim();
			if (execValue is not null && ExecReferencesPath(execValue, exePath))
			{
				// 世代マーカーも同じなら再生成不要
				if (lines.Any(l => l.Trim() == $"X-KEVI-Generated={marker}"))
					return;
				// 世代が変わっていても Exec のカスタマイズは引き継ぐ
				customExec = execValue;
			}
		}

		// ベースは存在確認済みなので配下の標準ディレクトリは作成する
		Directory.CreateDirectory(appsDir);
		File.WriteAllText(path, BuildDesktopContent(exePath, marker, customExec));
	}

	/// <summary>
	/// Exec 行の値が指定された実行ファイルパスをコマンドとして参照しているか確認する。
	/// 起動オプションや環境変数が設定されていても一致とみなすが、
	/// 前方一致する別バイナリのパス (例: 同名 + 接尾辞) は一致とみなさない。
	/// </summary>
	private static bool ExecReferencesPath(string execValue, string exePath)
	{
		var index = execValue.IndexOf(exePath, StringComparison.Ordinal);
		while (index >= 0)
		{
			// パスの前後が値の端・空白・引用符のいずれかであることを確認する
			var isStartBoundary = index == 0 || execValue[index - 1] is ' ' or '"' or '\'';
			var endIndex = index + exePath.Length;
			var isEndBoundary = endIndex >= execValue.Length || execValue[endIndex] is ' ' or '"' or '\'';
			if (isStartBoundary && isEndBoundary)
				return true;
			index = execValue.IndexOf(exePath, index + 1, StringComparison.Ordinal);
		}
		return false;
	}

	private static string BuildDesktopContent(string exePath, string marker, string? customExec)
	{
		var workingDir = Path.GetDirectoryName(exePath) ?? "";
		var execValue = customExec ?? $"\"{exePath}\"";
		return $"""
		[Desktop Entry]
		Type=Application
		Version=1.0
		Name={NotificationProvider.ApplicationName}
		GenericName=防災情報ビューア
		Comment=地震・津波・緊急地震速報などの防災情報を表示します
		Exec={execValue}
		Path={workingDir}
		Icon={NotificationProvider.ApplicationId}
		Terminal=false
		Categories=Utility;Network;
		StartupNotify=true
		StartupWMClass=KyoshinEewViewer
		Keywords=地震;津波;緊急地震速報;EEW;防災;
		X-KEVI-Generated={marker}

		""";
	}

	private static void InstallIcon(string dataHome)
	{
		var iconDir = Path.Combine(dataHome, "icons", "hicolor", "512x512", "apps");
		var iconPath = Path.Combine(iconDir, NotificationProvider.ApplicationId + ".png");
		if (File.Exists(iconPath))
			return;

		Directory.CreateDirectory(iconDir);
		using var asset = AssetLoader.Open(new Uri($"avares://KyoshinEewViewer.Desktop/Notification/Assets/{NotificationProvider.ApplicationId}.png"));
		using var file = File.Create(iconPath);
		asset.CopyTo(file);
	}
}
