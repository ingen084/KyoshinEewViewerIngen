using KyoshinEewViewer.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;

namespace KyoshinEewViewer.Notification.Linux
{
	public class LinuxNotificationProvider : NotificationProvider
	{
		public override bool TrayIconAvailable { get; } = false;
		private ILogger Logger { get; }
		public LinuxNotificationProvider()
		{
			Logger = LoggingService.CreateLogger(this);
		}

		// TODO Linux向けの処理は未実装
		public override void InitalizeTrayIcon(TrayMenuItem[] menuItems) { }
		public override void SendNotice(string title, string message)
		{
			try
			{
				Process.Start("notify-send", $"\"{title.Replace("\"", "\\\"")}\" \"{message.Replace("\"", "\\\"")}\"");
			}
			catch (Exception ex)
			{
				Logger.LogWarning("通知失敗: " + ex);
			}
		}
		public override void Dispose() => GC.SuppressFinalize(this);
	}
}
