using System;
using System.Runtime.InteropServices;

namespace KyoshinEewViewer.Desktop;

/// <summary>
/// Rust ランチャー経由で起動された場合に、起動完了をランチャーへ通知する。
/// ランチャーは環境変数 KEVI_SPLASH_EVENT で名前付きイベントを渡してくるため、
/// MainWindow 表示完了時に SetEvent してランチャー側スプラッシュを閉じさせる。
/// ランチャー非経由の通常起動では何もしない。
/// </summary>
internal static class LauncherBridge
{
	private static bool _notified;

	/// <summary>
	/// 起動完了をランチャーへ通知する。複数回呼ばれても最初の 1 回だけ有効。
	/// </summary>
	public static void NotifyStartupComplete()
	{
		if (_notified)
			return;
		_notified = true;

		var name = Environment.GetEnvironmentVariable("KEVI_SPLASH_EVENT");
		if (string.IsNullOrEmpty(name))
			return;
		if (!OperatingSystem.IsWindows())
			return;

		try
		{
			const uint EVENT_MODIFY_STATE = 0x0002;
			var handle = OpenEventW(EVENT_MODIFY_STATE, false, name);
			if (handle != IntPtr.Zero)
			{
				SetEvent(handle);
				CloseHandle(handle);
			}
		}
		catch
		{
			// ランチャー連携の失敗は起動に影響させない
		}
	}

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern IntPtr OpenEventW(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, string lpName);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool SetEvent(IntPtr hEvent);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool CloseHandle(IntPtr hObject);
}
