#if WINDOWS
using Avalonia.Platform;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Notification;
using Microsoft.Win32;
using Splat;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using static KyoshinEewViewer.Desktop.Notification.Windows.NativeMethods;

namespace KyoshinEewViewer.Desktop.Notification.Windows;

/// <summary>
/// Toast 通知をアプリ (AUMID) に紐付けるためのセットアップを行う。
/// <para>
/// 未パッケージ(ポータブル)アプリでは、AUMID に CustomActivator(COM) を登録しないと Windows がトーストを表示しない。
/// かつてはこの登録を Microsoft.Toolkit.Uwp.Notifications の Compat が肩代わりしていたが、
/// 依存(System.Drawing.Common)を排除するため WinRT を直接利用し、登録を自前で行う。
/// </para>
/// <para>
/// ポータブル配布のため永続的なフットプリントを残さないよう、レジストリは <see cref="RegistryOptions.Volatile"/> で作成する。
/// これによりログオフ/再起動でハイブがアンロードされると自動的に消えるため、アプリがクラッシュしても永続的なエントリは残らない。
/// 正常終了時は <see cref="Unregister"/> で即時削除する。
/// </para>
/// </summary>
internal static class WindowsToastIdentity
{
	/// <summary>
	/// Toast クリック活性化を受け取る COM アクティベーターの CLSID。
	/// AUMID が固定のため固定値とする。
	/// </summary>
	private static readonly Guid ActivatorClsid = new("A439E800-12C9-4225-8118-D45D07D7D371");

	private static string CustomActivatorKeyPath => $@"Software\Classes\AppUserModelId\{NotificationProvider.ApplicationId}";
	private static string ClsidKeyPath => $@"Software\Classes\CLSID\{{{ActivatorClsid}}}";

	[DllImport("shell32.dll", PreserveSig = false)]
	private static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string appID);

	/// <summary>COM アクティベーターの登録トークン (解除に使用)</summary>
	private static uint _classObjectCookie;

	/// <summary>
	/// プロセスの AUMID を設定し、COM アクティベーターとレジストリを登録して Toast を表示可能にする。
	/// あわせて Toast に表示するアプリ名・アイコンを整える。
	/// </summary>
	public static void Register()
	{
		try
		{
			// Compat の初期化より前にプロセス AUMID を設定する
			SetCurrentProcessExplicitAppUserModelID(NotificationProvider.ApplicationId);

			// 前回異常終了で消し損ねたエントリが残っている可能性があるため一度削除してから作り直す
			DeleteRegistryEntries();

			var exePath = Process.GetCurrentProcess().MainModule?.FileName;
			if (exePath is null)
			{
				LogHost.Default.Warn("実行ファイルパスの取得に失敗したため Toast 用登録を中止します");
				return;
			}

			// CLSID\{guid}\LocalServer32 に実行ファイルを登録する (未起動時のクリック活性化で起動される)
			using (var localServerKey = Registry.CurrentUser.CreateSubKey(
				ClsidKeyPath + @"\LocalServer32", RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryOptions.Volatile))
				localServerKey.SetValue(null, $"\"{exePath}\"", RegistryValueKind.String);

			// AppUserModelId に表示情報と CustomActivator を登録する
			using (var aumidKey = Registry.CurrentUser.CreateSubKey(
				CustomActivatorKeyPath, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryOptions.Volatile))
			{
				aumidKey.SetValue("DisplayName", NotificationProvider.ApplicationName, RegistryValueKind.String);
				aumidKey.SetValue("IconBackgroundColor", "FFDDDDDD", RegistryValueKind.String);
				var iconPath = ExtractIcon();
				if (iconPath is not null)
					aumidKey.SetValue("IconUri", iconPath, RegistryValueKind.String);
				// この値がないと未パッケージアプリでは Toast が表示されない
				aumidKey.SetValue("CustomActivator", $"{{{ActivatorClsid}}}", RegistryValueKind.String);
			}

			// COM アクティベーターを常駐させる。クリック時は in-process で受け取り、新規プロセス起動を防ぐ
			var hr = CoRegisterClassObject(ActivatorClsid, new NotificationActivatorClassFactory(),
				ClsctxLocalServer, RegclsMultipleuse, out _classObjectCookie);
			if (hr < 0)
				LogHost.Default.Warn($"COM アクティベーターの登録に失敗しました (HRESULT: 0x{hr:X8})");
		}
		catch (Exception ex)
		{
			LogHost.Default.Warn(ex, "Toast用 AUMID のセットアップに失敗しました");
		}
	}

	/// <summary>
	/// 登録した COM アクティベーター / レジストリエントリを削除する (終了時に呼ぶ)。
	/// </summary>
	public static void Unregister()
	{
		try
		{
			if (_classObjectCookie != 0)
			{
				CoRevokeClassObject(_classObjectCookie);
				_classObjectCookie = 0;
			}
			DeleteRegistryEntries();
		}
		catch (Exception ex)
		{
			LogHost.Default.Warn(ex, "Toast用 AUMID 登録の削除に失敗しました");
		}
	}

	private static void DeleteRegistryEntries()
	{
		Registry.CurrentUser.DeleteSubKeyTree(CustomActivatorKeyPath, throwOnMissingSubKey: false);
		Registry.CurrentUser.DeleteSubKeyTree(ClsidKeyPath, throwOnMissingSubKey: false);
	}

	private static string? ExtractIcon()
	{
		try
		{
			var dir = PlatformDirectories.ApplicationData;
			PlatformDirectories.EnsureDirectoryExists(dir);
			var path = Path.Combine(dir, "notification-icon.png");

			using (var asset = AssetLoader.Open(new Uri($"avares://KyoshinEewViewer.Desktop/Notification/Assets/{NotificationProvider.ApplicationId}.png")))
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

	/// <summary>
	/// Toast のクリック活性化を受け取る COM コールバック。
	/// 現状クリック時に固有の処理は行わないため空実装とする。
	/// </summary>
	[ClassInterface(ClassInterfaceType.None)]
	[ComVisible(true)]
	private sealed class NotificationActivator : INotificationActivationCallback
	{
		public void Activate(string appUserModelId, string invokedArgs, NotificationUserInputData[] data, uint dataCount) { }
	}

	/// <summary>
	/// <see cref="NotificationActivator"/> を生成する COM クラスファクトリ。
	/// </summary>
	private sealed class NotificationActivatorClassFactory : IClassFactory
	{
		private static readonly Guid IUnknownGuid = new("00000000-0000-0000-C000-000000000046");

		public int CreateInstance(IntPtr pUnkOuter, ref Guid riid, out IntPtr ppvObject)
		{
			ppvObject = IntPtr.Zero;
			if (pUnkOuter != IntPtr.Zero)
				return unchecked((int)0x80040110); // CLASS_E_NOAGGREGATION
			if (riid != typeof(INotificationActivationCallback).GUID && riid != IUnknownGuid)
				return unchecked((int)0x80004002); // E_NOINTERFACE
			ppvObject = Marshal.GetComInterfaceForObject(new NotificationActivator(), typeof(INotificationActivationCallback));
			return 0;
		}

		public int LockServer(bool fLock) => 0;
	}
}

/// <summary>Toast クリック時に渡されるユーザー入力の 1 項目</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NotificationUserInputData
{
	[MarshalAs(UnmanagedType.LPWStr)] public string Key;
	[MarshalAs(UnmanagedType.LPWStr)] public string Value;
}

/// <summary>Toast がクリックされたときに呼ばれる COM コールバック (INotificationActivationCallback)</summary>
[ComImport]
[Guid("53E31837-6600-4A81-9395-75CFFE746F94")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface INotificationActivationCallback
{
	void Activate(
		[In, MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
		[In, MarshalAs(UnmanagedType.LPWStr)] string invokedArgs,
		[In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] NotificationUserInputData[] data,
		[In, MarshalAs(UnmanagedType.U4)] uint dataCount);
}

/// <summary>COM クラスファクトリ (IClassFactory)</summary>
[ComImport]
[Guid("00000001-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IClassFactory
{
	[PreserveSig]
	int CreateInstance(IntPtr pUnkOuter, ref Guid riid, out IntPtr ppvObject);
	[PreserveSig]
	int LockServer(bool fLock);
}
#endif
