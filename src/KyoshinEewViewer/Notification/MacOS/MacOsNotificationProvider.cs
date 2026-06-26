#if !WINDOWS
using Splat;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace KyoshinEewViewer.Notification.MacOS;

public class MacOsNotificationProvider : NotificationProvider
{
	public override bool TrayIconAvailable { get; } = false;

	// TODO macOS向けのトレイアイコン処理は未実装
	public override void InitializeTrayIcon(TrayMenuItem[] menuItems) { }

	public override void SendNotice(NotificationRequest request)
	{
		try
		{
			// macOS には緊急度の概念が無いため Urgency は表示に反映されない
			DeliverNative(request);
		}
		catch (Exception ex)
		{
			LogHost.Default.Warn(ex, "NSUserNotification での通知に失敗したため osascript にフォールバックします");
			FallbackOsascript(request);
		}
	}

	#region Objective-C ランタイム interop (NSUserNotification)

	private const string ObjCLibrary = "/usr/lib/libobjc.dylib";

	[DllImport(ObjCLibrary, EntryPoint = "objc_getClass")]
	private static extern IntPtr GetClass([MarshalAs(UnmanagedType.LPStr)] string name);

	[DllImport(ObjCLibrary, EntryPoint = "sel_registerName")]
	private static extern IntPtr GetSelector([MarshalAs(UnmanagedType.LPStr)] string name);

	[DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
	private static extern IntPtr MsgSend(IntPtr receiver, IntPtr selector);

	[DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
	private static extern IntPtr MsgSend(IntPtr receiver, IntPtr selector, IntPtr arg1);

	private static void DeliverNative(NotificationRequest request)
	{
		var notificationClass = GetClass("NSUserNotification");
		var centerClass = GetClass("NSUserNotificationCenter");
		if (notificationClass == IntPtr.Zero || centerClass == IntPtr.Zero)
			throw new PlatformNotSupportedException("NSUserNotification を利用できません");

		var notification = MsgSend(MsgSend(notificationClass, GetSelector("alloc")), GetSelector("init"));
		if (notification == IntPtr.Zero)
			throw new InvalidOperationException("NSUserNotification の生成に失敗しました");

		MsgSend(notification, GetSelector("setTitle:"), ToNSString(request.Title));
		MsgSend(notification, GetSelector("setInformativeText:"), ToNSString(request.Message));
		// soundName は設定しない (= 無音)。通知音はアプリ側 (PlaySoundAction 等) が担う

		var center = MsgSend(centerClass, GetSelector("defaultUserNotificationCenter"));
		if (center == IntPtr.Zero)
			throw new InvalidOperationException("NSUserNotificationCenter を取得できません");

		MsgSend(center, GetSelector("deliverNotification:"), notification);
		// deliverNotification: 側で retain されるが、release は二重解放リスクを避けるため行わない
		// (まれな通知のため一時的なリークは許容)
	}

	private static IntPtr ToNSString(string value)
	{
		var utf8 = Encoding.UTF8.GetBytes(value);
		var ptr = Marshal.AllocHGlobal(utf8.Length + 1);
		try
		{
			Marshal.Copy(utf8, 0, ptr, utf8.Length);
			Marshal.WriteByte(ptr, utf8.Length, 0);
			// stringWithUTF8String: は autorelease されるため別途解放しない
			return MsgSend(GetClass("NSString"), GetSelector("stringWithUTF8String:"), ptr);
		}
		finally
		{
			Marshal.FreeHGlobal(ptr);
		}
	}

	#endregion

	private static void FallbackOsascript(NotificationRequest request)
	{
		try
		{
			var title = EscapeForAppleScript(request.Title);
			var message = EscapeForAppleScript(request.Message);
			Process.Start(new ProcessStartInfo
			{
				FileName = "osascript",
				ArgumentList = { "-e", $"display notification \"{message}\" with title \"{title}\"" },
				UseShellExecute = false,
			});
		}
		catch (Exception ex)
		{
			LogHost.Default.Warn(ex, "通知失敗");
		}
	}

	private static string EscapeForAppleScript(string value)
		=> value.Replace("\\", "\\\\").Replace("\"", "\\\"");

	public override void Dispose() => GC.SuppressFinalize(this);
}
#endif
