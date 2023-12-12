using Avalonia.Controls;
using Avalonia.Threading;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.Map;
using ReactiveUI;
using Splat;
using System;
using System.Linq;
using System.Reactive.Linq;

namespace KyoshinEewViewer.Views;
public partial class MainView : UserControl
{
	public MainView()
	{
		InitializeComponent();

		KyoshinEewViewerApp.Selector?.WhenAnyValue(x => x.SelectedWindowTheme).Where(x => x != null)
				.Subscribe(x => Map.RefreshResourceCache());

		var config = Locator.Current.RequireService<KyoshinEewViewerConfiguration>();
		config.Map.WhenAnyValue(x => x.DisableManualMapControl).Subscribe(x =>
		{
			HomeButton.IsVisible = !x;
			Map.IsDisableManualControl = x;
		});
		HomeButton.IsVisible = !config.Map.DisableManualMapControl;
		Map.IsDisableManualControl = config.Map.DisableManualMapControl;

		Map.Zoom = 6;
		Map.CenterLocation = new KyoshinMonitorLib.Location(36.474f, 135.264f);

		Map.WhenAnyValue(m => m.CenterLocation, m => m.Zoom).Sample(TimeSpan.FromSeconds(.1)).Subscribe(m =>
		{
			var config = Locator.Current.RequireService<KyoshinEewViewerConfiguration>();
			Dispatcher.UIThread.Post(new Action(() =>
			{
				MiniMapContainer.IsVisible = config.Map.UseMiniMap && Map.IsNavigatedPosition(new RectD(config.Map.Location1.CastPoint(), config.Map.Location2.CastPoint()));
				ResetMinimapPosition();
			}));
		});

		MiniMap.WhenAnyValue(m => m.Bounds).Subscribe(b => ResetMinimapPosition());
		AttachedToVisualTree += (s, e) => 
		{
			ResetMinimapPosition();
		};

		MessageBus.Current.Listen<MapNavigationRequested>().Subscribe(x =>
		{
			if (!config.Map.AutoFocus)
				return;
			if (x.Bound is { } rect)
			{
				if (x.MustBound is { } mustBound)
					Map.Navigate(rect, config.Map.AutoFocusAnimation ? TimeSpan.FromSeconds(.3) : TimeSpan.Zero, mustBound);
				else
					Map.Navigate(rect, config.Map.AutoFocusAnimation ? TimeSpan.FromSeconds(.3) : TimeSpan.Zero);
			}
			else
				NavigateToHome();
		});
		MessageBus.Current.Listen<RegistMapPositionRequested>().Subscribe(x =>
		{
			var halfPaddedRect = new PointD(Map.PaddedRect.Width / 2, -Map.PaddedRect.Height / 2);
			var centerPixel = Map.CenterLocation.ToPixel(Map.Zoom);

			config.Map.Location1 = (centerPixel + halfPaddedRect).ToLocation(Map.Zoom);
			config.Map.Location2 = (centerPixel - halfPaddedRect).ToLocation(Map.Zoom);
		});

		AttachedToVisualTree += (s, e) =>
		{
			if (TopLevel.GetTopLevel(this) is { } topLevel && topLevel.InsetsManager is { } insetsManager)
			{
				insetsManager.IsSystemBarVisible = false;
				insetsManager.DisplayEdgeToEdge = true;
			}
		};
	}

	private void ResetMinimapPosition()
	{
		if (!MiniMap.IsVisible)
			return;
		//MiniMap.Navigate(new RectD(new PointD(24.127, 123.585), new PointD(28.546, 129.803)), TimeSpan.Zero, true);
		MiniMap.Navigate(new RectD(new PointD(22.289, 121.207), new PointD(31.128, 132.100)), TimeSpan.Zero, true);
	}

	private void NavigateToHome()
	{
		var config = Locator.Current.RequireService<KyoshinEewViewerConfiguration>();
		Map?.Navigate(
			new RectD(config.Map.Location1.CastPoint(), config.Map.Location2.CastPoint()),
			config.Map.AutoFocusAnimation ? TimeSpan.FromSeconds(.3) : TimeSpan.Zero);
	}
}
