using System;
using System.Runtime.InteropServices;

namespace KyoshinEewViewer.Notification
{
	public abstract class TrayIcon : IDisposable
	{
		protected TrayMenuItem[] TrayMenuItems { get; }
		public TrayIcon(TrayMenuItem[] menuItems)
		{
			TrayMenuItems = menuItems;
		}

		public abstract void SendNotice(string title, string message);
		public abstract void Dispose();

		public static TrayIcon? CreateTrayIcon(TrayMenuItem[] menuItems)
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				return new Windows.TrayIconImpl(menuItems);
			return null;
		}
	}
}
