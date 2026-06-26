#if WINDOWS
using Avalonia.Platform;
using KyoshinEewViewer.Core;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Win32;
using Splat;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace KyoshinEewViewer.Notification.Windows;

/// <summary>
/// Toast 通知をアプリ (AUMID) に紐付けるためのセットアップを行う。
/// <para>
/// 未パッケージ(ポータブル)アプリでは、AUMID に CustomActivator(COM) を登録しないと Windows がトーストを表示しない。
/// この登録は <see cref="ToastNotificationManagerCompat"/> が行うため、それを利用する。
/// プロセスの AUMID を自前の値に設定してから Compat を初期化することで、自前 AUMID に紐付く。
/// </para>
/// <para>
/// ポータブル配布のため永続的なフットプリントを残さないよう、終了時に <see cref="ToastNotificationManagerCompat.Uninstall"/> で登録を削除する。
/// </para>
/// </summary>
internal static class WindowsToastIdentity
{
	[DllImport("shell32.dll", PreserveSig = false)]
	private static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string appID);

	/// <summary>
	/// プロセスの AUMID を設定し、Compat を初期化して Toast を表示可能にする。
	/// あわせて Toast に表示するアプリ名・アイコンを整える。
	/// </summary>
	public static void Register()
	{
		try
		{
			// Compat の初期化より前にプロセス AUMID を設定する (Compat はプロセスの AUMID を採用する)
			SetCurrentProcessExplicitAppUserModelID(NotificationProvider.ApplicationId);

			// Compat に初回アクセスすると CustomActivator(COM) と AppUserModelId レジストリが登録される
			_ = ToastNotificationManagerCompat.CreateToastNotifier();

			// Compat が書き込んだ DisplayName / IconUri を自前の値で上書きして見栄えを整える
			using var key = Registry.CurrentUser.OpenSubKey(
				$@"Software\Classes\AppUserModelId\{NotificationProvider.ApplicationId}", writable: true);
			if (key is null)
				return;

			key.SetValue("DisplayName", NotificationProvider.ApplicationName, RegistryValueKind.String);
			var iconPath = ExtractIcon();
			if (iconPath is not null)
				key.SetValue("IconUri", iconPath, RegistryValueKind.String);
		}
		catch (Exception ex)
		{
			LogHost.Default.Warn(ex, "Toast用 AUMID のセットアップに失敗しました");
		}
	}

	/// <summary>
	/// 登録した AUMID / COM アクティベーターを削除する (終了時に呼ぶ)。
	/// </summary>
	public static void Unregister()
	{
		try
		{
			ToastNotificationManagerCompat.Uninstall();
		}
		catch (Exception ex)
		{
			LogHost.Default.Warn(ex, "Toast用 AUMID 登録の削除に失敗しました");
		}
	}

	private static string? ExtractIcon()
	{
		try
		{
			var dir = PlatformDirectories.ApplicationData;
			PlatformDirectories.EnsureDirectoryExists(dir);
			var path = Path.Combine(dir, "notification-icon.png");

			using (var asset = AssetLoader.Open(new Uri($"avares://KyoshinEewViewer/Notification/Assets/{NotificationProvider.ApplicationId}.png")))
			using (var file = File.Create(path))
				asset.CopyTo(file);
			return path;
		}
		catch (Exception ex)
		{
			LogHost.Default.Warn(ex, "Toast用アイコンの展開に失敗しました");
			return null;
		}
	}
}
#endif
