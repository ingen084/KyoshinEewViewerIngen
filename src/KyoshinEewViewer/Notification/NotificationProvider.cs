using System;
using System.Runtime.InteropServices;

namespace KyoshinEewViewer.Notification;

public abstract class NotificationProvider : IDisposable
{
	public abstract bool TrayIconAvailable { get; }

	public abstract void InitalizeTrayIcon(TrayMenuItem[] menuItems);
	public abstract void SendNotice(string title, string message);
	public abstract void Dispose();

	public static NotificationProvider? CreateTrayIcon()
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			return new Windows.WindowsNotificationProvider();
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			return new Linux.LinuxNotificationProvider();
		return null;
	}
}
