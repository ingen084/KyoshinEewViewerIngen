using Avalonia.Platform;
using KyoshinEewViewer.Notification;
using Splat;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

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
			LogHost.Default.Warn(ex, "デスクトップエントリの生成に失敗しました");
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

		// Exec が現在のバイナリパスと一致し、世代マーカーも同じなら再生成不要
		if (File.Exists(path))
		{
			var existing = File.ReadAllText(path);
			if (existing.Contains($"Exec=\"{exePath}\"") && existing.Contains($"X-KEVI-Generated={marker}"))
				return;
		}

		// ベースは存在確認済みなので配下の標準ディレクトリは作成する
		Directory.CreateDirectory(appsDir);
		File.WriteAllText(path, BuildDesktopContent(exePath, marker));
	}

	private static string BuildDesktopContent(string exePath, string marker)
		=> $"""
		[Desktop Entry]
		Type=Application
		Version=1.0
		Name={NotificationProvider.ApplicationName}
		GenericName=防災情報ビューア
		Comment=地震・津波・緊急地震速報などの防災情報を表示します
		Exec="{exePath}"
		Icon={NotificationProvider.ApplicationId}
		Terminal=false
		Categories=Utility;Network;
		StartupNotify=true
		StartupWMClass=KyoshinEewViewer
		Keywords=地震;津波;緊急地震速報;EEW;防災;
		X-KEVI-Generated={marker}

		""";

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
