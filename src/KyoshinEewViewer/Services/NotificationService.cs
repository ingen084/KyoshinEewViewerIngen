using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.Notification;
using ReactiveUI;
using System;

namespace KyoshinEewViewer.Services
{
	public class NotificationService
	{
		private static NotificationService? _default;

		public static NotificationService Default => _default ??= new NotificationService();

		private NotificationProvider? TrayIcon { get; set; }
		public bool Available => TrayIcon != null;//NotifyIconService?.Enabled ?? false;

		public NotificationService()
		{
			//NotificationManager.Initialize("net.ingen084.kyoshineewviewer", "KyoshinEewViewer for ingen");
			//NotificationManager.NotificationIconSelectedEvent += c => MessageBus.Current.SendMessage(new ShowMainWindowRequested());

			MessageBus.Current.Listen<ApplicationClosing>().Subscribe(x => TrayIcon?.Dispose());
		}

		public void Initalize()
		{
			TrayIcon = NotificationProvider.CreateTrayIcon();
			if (TrayIcon == null)
				return;
			if (ConfigurationService.Default.Notification.TrayIconEnable)
				TrayIcon.InitalizeTrayIcon(new[]
				{
					new TrayMenuItem("メインウィンドウを開く(&O)", () => MessageBus.Current.SendMessage(new ShowMainWindowRequested())),
					new TrayMenuItem("設定(&S)", () => MessageBus.Current.SendMessage(new ShowSettingWindowRequested())),
					new TrayMenuItem("終了(&E)", () => App.MainWindow?.Close()),
				});
		}

		public void Notify(string title, string message)
		{
			if (Available && ConfigurationService.Default.Notification.Enable)
				TrayIcon?.SendNotice(title, message);
		}
	}
}
