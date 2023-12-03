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
	private bool IsFullScreen { get; set; }
	private WindowState LatestWindowState { get; set; }

	/// <summary>
	/// クラッシュしたときにウィンドウ位置を記録しておくようのタイマー
	/// </summary>
	public Timer SaveTimer { get; }

	public MainWindow()
	{
		InitializeComponent();

		var config = Locator.Current.RequireService<KyoshinEewViewerConfiguration>();
		if (config.WindowSize is { } size)
			ClientSize = new Size(size.X, size.Y);
		WindowState = config.WindowState;
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

			if (IsFullScreen)
			{
				WindowState = WindowState.Normal;
				IsFullScreen = false;
				return;
			}
			WindowState = WindowState.FullScreen;
			IsFullScreen = true;
		};
		Closing += (s, e) =>
		{
			if (e.CloseReason == WindowCloseReason.WindowClosing && config.Notification.HideWhenClosingWindow && (Locator.Current.GetService<NotificationService>()?.TrayIconAvailable ?? false))
			{
				Hide();
				if (!IsHideAnnounced)
				{
					Locator.Current.GetService<NotificationService>()?.Notify("タスクトレイに格納しました", "アプリケーションは実行中です");
					IsHideAnnounced = true;
				}
				e.Cancel = true;
				return;
			}
			SaveConfig();
		};
		this.WhenAnyValue(w => w.WindowState).Subscribe(s =>
		{
			if (s == WindowState.Minimized && config.Notification.HideWhenMinimizeWindow && (Locator.Current.GetService<NotificationService>()?.TrayIconAvailable ?? false))
			{
				Hide();
				if (!IsHideAnnounced)
				{
					Locator.Current.GetService<NotificationService>()?.Notify("タスクトレイに格納しました", "アプリケーションは実行中です");
					IsHideAnnounced = true;
				}
				return;
			}
			LatestWindowState = s;
		});

		MessageBus.Current.Listen<Core.Models.Events.ShowSettingWindowRequested>().Subscribe(x => Locator.Current.GetService<SubWindowsService>()?.ShowSettingWindow());
		MessageBus.Current.Listen<Core.Models.Events.ShowMainWindowRequested>().Subscribe(x =>
		{
			Topmost = true;
			Show();
			WindowState = LatestWindowState;
			Topmost = false;
		});

		SaveTimer = new Timer(_ => Dispatcher.UIThread.InvokeAsync(SaveConfig), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(10));
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
		if (WindowState != WindowState.Minimized)
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
