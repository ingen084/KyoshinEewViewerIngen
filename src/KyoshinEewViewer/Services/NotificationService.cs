using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.Notification;
using ReactiveUI;
using Splat;
using System;

namespace KyoshinEewViewer.Services;

public class NotificationService
{
	private KyoshinEewViewerConfiguration Config { get; }
	private NotificationProvider? TrayIcon { get; set; }
	public bool Available => TrayIcon != null;//NotifyIconService?.Enabled ?? false;
	public bool TrayIconAvailable => TrayIcon?.TrayIconAvailable ?? false;

	public NotificationService(KyoshinEewViewerConfiguration config)
	{
		SplatRegistrations.RegisterLazySingleton<NotificationService>();

		Config = config;

		//NotificationManager.Initialize("net.ingen084.kyoshineewviewer", "KyoshinEewViewer for ingen");
		//NotificationManager.NotificationIconSelectedEvent += c => MessageBus.Current.SendMessage(new ShowMainWindowRequested());

		MessageBus.Current.Listen<ApplicationClosing>().Subscribe(x => TrayIcon?.Dispose());
	}

	public void Initialize()
	{
		TrayIcon = NotificationProvider.CreateTrayIcon();
		if (TrayIcon == null)
			return;
		if (Config.Notification.TrayIconEnable)
			TrayIcon.InitializeTrayIcon([
				new TrayMenuItem("メインウィンドウを開く(&O)", () => MessageBus.Current.SendMessage(new ShowMainWindowRequested())),
				new TrayMenuItem("設定(&S)", () => MessageBus.Current.SendMessage(new ShowSettingWindowRequested())),
				new TrayMenuItem("終了(&E)", () => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown()),
			]);
	}

	public void Notify(string title, string message)
	{
		if (Available && Config.Notification.Enable)
			TrayIcon?.SendNotice(title, message);
	}
}
