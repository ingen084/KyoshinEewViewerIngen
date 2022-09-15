using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.ViewModels;
using ReactiveUI;
using Splat;
using System;
using System.Reactive.Linq;

namespace KyoshinEewViewer.Views;

public partial class MainWindow : Window
{
	private bool IsFullScreen { get; set; }

	public MainWindow()
	{
		InitializeComponent();

		WindowState = ConfigurationService.Current.WindowState;
		if (ConfigurationService.Current.WindowLocation is Core.Models.KyoshinEewViewerConfiguration.Point2D position && position.X != -32000 && position.Y != -32000)
		{
			Position = new PixelPoint((int)position.X, (int)position.Y);
			WindowStartupLocation = WindowStartupLocation.Manual;
		}
		if (ConfigurationService.Current.WindowSize is Core.Models.KyoshinEewViewerConfiguration.Point2D size)
			ClientSize = new Size(size.X, size.Y);

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
			if (ConfigurationService.Current.Notification.HideWhenClosingWindow && (Locator.Current.GetService<NotificationService>()?.TrayIconAvailable ?? false))
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
			if (s == WindowState.Minimized && ConfigurationService.Current.Notification.HideWhenMinimizeWindow && (Locator.Current.GetService<NotificationService>()?.TrayIconAvailable ?? false))
			{
				Hide();
				if (!IsHideAnnounced)
				{
					Locator.Current.GetService<NotificationService>()?.Notify("タスクトレイに格納しました", "アプリケーションは実行中です");
					IsHideAnnounced = true;
				}
			}
		});

		MessageBus.Current.Listen<Core.Models.Events.ShowSettingWindowRequested>().Subscribe(x => SubWindowsService.Default.ShowSettingWindow());
		MessageBus.Current.Listen<Core.Models.Events.ShowMainWindowRequested>().Subscribe(x =>
		{
			Topmost = true;
			Show();
			Topmost = false;
		});
	}

	private bool IsHideAnnounced { get; set; }

	public new void Close()
	{
		SaveConfig();
		base.Close();
	}

	private void SaveConfig()
	{
		ConfigurationService.Current.WindowState = WindowState;
		if (WindowState != WindowState.Minimized)
		{
			ConfigurationService.Current.WindowLocation = new(Position.X, Position.Y);
			if (WindowState != WindowState.Maximized)
				ConfigurationService.Current.WindowSize = new(ClientSize.Width, ClientSize.Height);
		}
		if (DataContext is MainWindowViewModel vm && !StartupOptions.IsStandalone)
			ConfigurationService.Current.SelectedTabName = vm.SelectedSeries?.Name;
		ConfigurationService.Save();
	}
}
