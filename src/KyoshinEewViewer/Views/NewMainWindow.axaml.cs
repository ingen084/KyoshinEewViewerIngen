using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.ViewModels;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Views;

public partial class NewMainWindow : CoreWindow
{
	private bool IsFullScreen { get; set; }

	public NewMainWindow()
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

		MessageBus.Current.Listen<Core.Models.Events.ShowSettingWindowRequested>().Subscribe(x => SubWindowsService.Default.ShowSettingWindow());
		MessageBus.Current.Listen<Core.Models.Events.ShowMainWindowRequested>().Subscribe(x =>
		{
			Topmost = true;
			Show();
			Topmost = false;
		});
	}
}
