using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.Messaging;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.Notification;
using Splat;
using System;

namespace KyoshinEewViewer.Services;

public class NotificationService
{
	private KyoshinEewViewerConfiguration Config { get; }

	private NotificationProvider? Notifier { get; set; }
	private TrayIconProvider? TrayIcon { get; set; }

	/// <summary>ウィンドウをトレイに格納しても復帰可能か (トレイが確実に表示される環境か)</summary>
	public bool CanHideToTray => TrayIcon?.CanHideToTray ?? false;

	public NotificationService(KyoshinEewViewerConfiguration config)
	{
		SplatRegistrations.RegisterLazySingleton<NotificationService>();

		Config = config;

		StrongReferenceMessenger.Default.Register<ApplicationClosing>(this, (_, x) =>
		{
			TrayIcon?.Dispose();
			Notifier?.Dispose();
		});
	}

	public void Initialize()
	{
		Notifier = Locator.Current.GetService<NotificationProvider>();
		TrayIcon = Locator.Current.GetService<TrayIconProvider>();

		// Linux ではデスクトップエントリを生成し、KDE 等でアプリ名/アイコン/通知設定を解決できるようにする
		if (Notifier != null && Config.Notification.RegisterDesktopEntry)
			Notifier.RegisterDesktopIntegration();

		if (TrayIcon != null && Config.Notification.TrayIconEnable)
			TrayIcon.Show([
				new TrayMenuItem("メインウィンドウを開く", () => StrongReferenceMessenger.Default.Send(new ShowMainWindowRequested())),
				new TrayMenuItem("設定", () => StrongReferenceMessenger.Default.Send(new ShowSettingWindowRequested())),
				new TrayMenuItem("終了", () => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown()),
			]);
	}

	public void Notify(NotificationRequest request)
	{
		if (Notifier != null && Config.Notification.Enable)
			Notifier.SendNotice(request);
	}

	public void Notify(string title, string message, NotificationUrgency urgency = NotificationUrgency.Normal)
		=> Notify(new NotificationRequest(title, message, urgency));
}
