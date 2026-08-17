using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Desktop.Services;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.ViewModels;
using R3;
using System;
using System.Reactive.Linq;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace KyoshinEewViewer.Desktop.Views;

public partial class MainWindow : Window
{
	private WindowState LastWindowState { get; set; }

	/// <summary>
	/// クラッシュしたときにウィンドウ位置を記録しておくようのタイマー
	/// </summary>
	public Timer SaveTimer { get; }

	private WindowPlacementTracker PlacementTracker { get; }

	public MainWindow()
	{
		InitializeComponent();

		var config = ServiceLocator.Current.RequireService<KyoshinEewViewerConfiguration>();
		var notificationService = ServiceLocator.Current.GetService<NotificationService>();

		// ウィンドウ位置の復元
		if (config.WindowSize is { } size)
			ClientSize = new Size(size.X, size.Y);
		WindowState = config.Notification.MinimizeWindowOnStartup ? WindowState.Minimized : config.WindowState;
		if (config.WindowLocation is { } position && WindowPlacementTracker.IsValidLocation(this, position))
		{
			WindowStartupLocation = WindowStartupLocation.Manual;
			Position = new PixelPoint((int)position.X, (int)position.Y);
		}
		PlacementTracker = new WindowPlacementTracker(this, config);

		// フルスクリーンモード
		KeyDown += (s, e) =>
		{
			if (e.Key != Key.F11)
				return;

			if (WindowState == WindowState.FullScreen)
			{
				WindowState = WindowState.Normal;
				return;
			}
			WindowState = WindowState.FullScreen;
		};
		Closing += (s, e) =>
		{
			// マルチウィンドウ有効時はタスクトレイ格納を無効化
			if (e.CloseReason == WindowCloseReason.WindowClosing && !config.MultiWindow.Enable && config.Notification.HideWhenClosingWindow && (notificationService?.CanHideToTray ?? false))
			{
				Hide();
				if (!IsHideAnnounced && config.Notification.HideToTrayNotify)
				{
					notificationService?.Notify("タスクトレイに格納しました", "アプリケーションは実行中です");
					IsHideAnnounced = true;
				}
				e.Cancel = true;
				return;
			}

			// サブウィンドウのクローズ時に設定を削除しないようにする
			var subWindowsService = ServiceLocator.Current.GetService<ISubWindowsService>();
			if (subWindowsService != null)
				subWindowsService.IsShuttingDown = true;

			SaveConfig();
		};
		this.ObservePropertyChanged(w => w.WindowState).Delay(TimeSpan.FromMilliseconds(200)).Subscribe(s => Dispatcher.UIThread.Post(() =>
		{
			// マルチウィンドウ有効時はタスクトレイ格納を無効化
			if (s == WindowState.Minimized && !config.MultiWindow.Enable && config.Notification.HideWhenMinimizeWindow && (notificationService?.CanHideToTray ?? false))
			{
				Hide();
				if (!IsHideAnnounced && config.Notification.HideToTrayNotify)
				{
					notificationService?.Notify("タスクトレイに格納しました", "アプリケーションは実行中です");
					IsHideAnnounced = true;
				}
				return;
			}
			LastWindowState = s;
		}));

		StrongReferenceMessenger.Default.Register<Core.Models.Events.ShowSettingWindowRequested>(this, (_, x) => Dispatcher.UIThread.Post(() => ServiceLocator.Current.GetService<ISubWindowsService>()?.ShowSettingWindow()));
		StrongReferenceMessenger.Default.Register<Core.Models.Events.DebugWindowOpenRequested>(this, (_, x) => Dispatcher.UIThread.Post(() => ServiceLocator.Current.GetService<ISubWindowsService>()?.ShowDebugWindow()));
		StrongReferenceMessenger.Default.Register<Core.Models.Events.ShowMainWindowRequested>(this, (_, x) =>
		{
			Dispatcher.UIThread.Post(() =>
			{
				Topmost = true;
				Show();
				WindowState = LastWindowState;
				Topmost = false;
			});
		});

		SaveTimer = new Timer(_ => Dispatcher.UIThread.Post(SaveConfig), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(10));
	}

	private bool IsHideAnnounced { get; set; }

	public new void Close()
	{
		SaveConfig();
		base.Close();
	}

	private void SaveConfig()
	{
		var config = ServiceLocator.Current.RequireService<KyoshinEewViewerConfiguration>();
		PlacementTracker.Save();
		if (DataContext is MainViewModel vm && StartupOptions.Current?.StandaloneSeriesName == null)
			config.SelectedTabName = vm.SelectedSeries?.Meta.Key;
		ConfigurationLoader.Save(config);
	}
}
