using Splat;
using System;
using System.Diagnostics;

namespace KyoshinEewViewer.Notification.Linux;

public class LinuxNotificationProvider : NotificationProvider
{
	public override bool TrayIconAvailable { get; } = false;

	// TODO Linux向けの処理は未実装
	public override void InitializeTrayIcon(TrayMenuItem[] menuItems) { }
	public override void SendNotice(string title, string message)
	{
		try
		{
			Process.Start("notify-send", $"\"[KEVi] {title.Replace("\"", "\\\"")}\" \"{message.Replace("\"", "\\\"")}\"");
		}
		catch (Exception ex)
		{
			LogHost.Default.Warn(ex, "通知失敗");
		}
	}
	public override void Dispose() => GC.SuppressFinalize(this);
}
