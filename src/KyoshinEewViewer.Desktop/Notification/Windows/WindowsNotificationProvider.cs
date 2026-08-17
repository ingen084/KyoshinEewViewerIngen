#if WINDOWS
using KyoshinEewViewer.Notification;
using System;
using System.Security;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using Microsoft.Extensions.Logging;
using KyoshinEewViewer.Core;

namespace KyoshinEewViewer.Desktop.Notification.Windows;

public class WindowsNotificationProvider : NotificationProvider
{
	public WindowsNotificationProvider()
		// Toast をアプリ名/アイコン付きで表示し通知設定に登録するため AUMID を登録する
		=> WindowsToastIdentity.Register();

	public override void SendNotice(NotificationRequest request)
	{
		try
		{
			// 通知音はアプリ側 (PlaySoundAction 等) が担うため OS 側は無音にする
			// 重要通知は画面に残り続けるよう Reminder シナリオにする
			var scenario = request.Urgency == NotificationUrgency.Critical ? " scenario=\"reminder\"" : "";
			var xmlText =
				$"<toast{scenario}>" +
					"<visual><binding template=\"ToastGeneric\">" +
						$"<text>{SecurityElement.Escape(request.Title)}</text>" +
						$"<text>{SecurityElement.Escape(request.Message)}</text>" +
					"</binding></visual>" +
					"<audio silent=\"true\" />" +
				"</toast>";

			var xml = new XmlDocument();
			xml.LoadXml(xmlText);

			// WindowsToastIdentity で登録した AUMID (CustomActivator) 宛てに表示する
			ToastNotificationManager.CreateToastNotifier(NotificationProvider.ApplicationId)
				.Show(new ToastNotification(xml));
		}
		catch (Exception ex)
		{
			AppLog.Default.LogWarning(ex, "トースト通知に失敗しました");
		}
	}

	public override void Dispose()
	{
		// ポータブル配布のため AUMID 登録を残さず削除する
		WindowsToastIdentity.Unregister();
		GC.SuppressFinalize(this);
	}
}
#endif
