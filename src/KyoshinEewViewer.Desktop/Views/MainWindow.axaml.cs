using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Threading;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Desktop.Services;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.ViewModels;
using R3;
using ReactiveUI;
using Splat;
using System;
using System.Reactive.Linq;
using System.Threading;

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

		var config = Locator.Current.RequireService<KyoshinEewViewerConfiguration>();
		var notificationService = Locator.Current.GetService<NotificationService>();

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
			var subWindowsService = Locator.Current.GetService<ISubWindowsService>();
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

		MessageBus.Current.Listen<Core.Models.Events.ShowSettingWindowRequested>().Subscribe(x => Dispatcher.UIThread.Post(() => Locator.Current.GetService<ISubWindowsService>()?.ShowSettingWindow()));
		MessageBus.Current.Listen<Core.Models.Events.DebugWindowOpenRequested>().Subscribe(x => Dispatcher.UIThread.Post(() => Locator.Current.GetService<ISubWindowsService>()?.ShowDebugWindow()));
		MessageBus.Current.Listen<Core.Models.Events.ShowMainWindowRequested>().Subscribe(x =>
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
		var config = Locator.Current.RequireService<KyoshinEewViewerConfiguration>();
		PlacementTracker.Save();
		if (DataContext is MainViewModel vm && StartupOptions.Current?.StandaloneSeriesName == null)
			config.SelectedTabName = vm.SelectedSeries?.Meta.Key;
		ConfigurationLoader.Save(config);
	}
}
