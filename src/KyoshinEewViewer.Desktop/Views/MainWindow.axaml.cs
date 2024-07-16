using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Desktop.Services;
using KyoshinEewViewer.Series;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.ViewModels;
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

	public MainWindow()
	{
		InitializeComponent();

		var config = Locator.Current.RequireService<KyoshinEewViewerConfiguration>();
		var notificationService = Locator.Current.GetService<NotificationService>();

		// ウィンドウ位置の復元
		if (config.WindowSize is { } size)
			ClientSize = new Size(size.X, size.Y);
		WindowState = config.Notification.MinimizeWindowOnStartup ? WindowState.Minimized : config.WindowState;
		if (config.WindowLocation is { } position && position.X != -32000 && position.Y != -32000)
		{
			WindowStartupLocation = WindowStartupLocation.Manual;
			Position = new PixelPoint((int)position.X, (int)position.Y);
		}

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
			if (e.CloseReason == WindowCloseReason.WindowClosing && config.Notification.HideWhenClosingWindow && (notificationService?.TrayIconAvailable ?? false))
			{
				Hide();
				if (!IsHideAnnounced)
				{
					notificationService?.Notify("タスクトレイに格納しました", "アプリケーションは実行中です");
					IsHideAnnounced = true;
				}
				e.Cancel = true;
				return;
			}
			SaveConfig();
		};
		this.WhenAnyValue(w => w.WindowState).Delay(TimeSpan.FromMilliseconds(200)).Subscribe(s => Dispatcher.UIThread.Post(() =>
		{
			if (s == WindowState.Minimized && config.Notification.HideWhenMinimizeWindow && (notificationService?.TrayIconAvailable ?? false))
			{
				Hide();
				if (!IsHideAnnounced)
				{
					notificationService?.Notify("タスクトレイに格納しました", "アプリケーションは実行中です");
					IsHideAnnounced = true;
				}
				return;
			}
			LastWindowState = s;
		}));

		MessageBus.Current.Listen<Core.Models.Events.ShowSettingWindowRequested>().Subscribe(x => Locator.Current.GetService<SubWindowsService>()?.ShowSettingWindow());
		MessageBus.Current.Listen<Core.Models.Events.ShowMainWindowRequested>().Subscribe(x =>
		{
			Topmost = true;
			Show();
			WindowState = LastWindowState;
			Topmost = false;
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
		config.WindowState = WindowState;
		if (WindowState is not WindowState.Minimized and not WindowState.FullScreen)
		{
			config.WindowLocation = new KyoshinEewViewerConfiguration.Point2D(Position.X, Position.Y);
			if (WindowState != WindowState.Maximized)
				config.WindowSize = new KyoshinEewViewerConfiguration.Point2D(ClientSize.Width, ClientSize.Height);
		}
		if (DataContext is MainViewModel vm && StartupOptions.Current?.StandaloneSeriesName == null)
			config.SelectedTabName = (vm.SelectedSeries as SeriesBase)?.Meta.Key;
		ConfigurationLoader.Save(config);
	}
}
