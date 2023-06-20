using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.Map;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;

namespace KyoshinEewViewer.Views;
public partial class MainView : UserControl
{
	public MainView()
	{
		InitializeComponent();

		var config = Locator.Current.RequireService<KyoshinEewViewerConfiguration>();
		config.Map.WhenAnyValue(x => x.DisableManualMapControl).Subscribe(x =>
		{
			HomeButton.IsVisible = !x;
		});
		HomeButton.IsVisible = !config.Map.DisableManualMapControl;

		// �}�b�v�܂��̃n���h��
		App.Selector?.WhenAnyValue(x => x.SelectedWindowTheme).Where(x => x != null)
				.Subscribe(x => Map.RefreshResourceCache());
		KyoshinMonitorLib.Location GetLocation(Point p)
		{
			var centerPix = Map!.CenterLocation.ToPixel(Map.Zoom);
			var originPix = new PointD(centerPix.X + ((Map.PaddedRect.Width / 2) - p.X) + Map.PaddedRect.Left, centerPix.Y + ((Map.PaddedRect.Height / 2) - p.Y) + Map.PaddedRect.Top);
			return originPix.ToLocation(Map.Zoom);
		}
		Map.PointerPressed += (s, e) =>
		{
			var originPos = e.GetCurrentPoint(Map).Position;
			StartPoints[e.Pointer] = originPos;
			// 3点以上の場合は2点になるようにする
			if (StartPoints.Count > 2)
				foreach (var pointer in StartPoints.Where(p => p.Key != e.Pointer).Select(p => p.Key).ToArray())
				{
					if (StartPoints.Count <= 2)
						break;
					StartPoints.Remove(pointer);
				}
		};
		Map.PointerMoved += (s, e) =>
		{
			if (!StartPoints.ContainsKey(e.Pointer))
				return;
			var newPosition = e.GetCurrentPoint(Map).Position;
			var beforePoint = StartPoints[e.Pointer];
			var vector = beforePoint - newPosition;
			if (vector == Vector.Zero)
				return;
			StartPoints[e.Pointer] = newPosition;

			if (config.Map.DisableManualMapControl || Map.IsNavigating)
				return;

			if (StartPoints.Count <= 1)
				Map.CenterLocation = (Map.CenterLocation.ToPixel(Map.Zoom) + (PointD)vector).ToLocation(Map.Zoom);
			else
			{
				var lockPos = StartPoints.First(p => p.Key != e.Pointer).Value;

				var befLen = GetLength(lockPos - beforePoint);
				var newLen = GetLength(lockPos - newPosition);
				var lockLoc = GetLocation(lockPos);

				var df = (befLen > newLen ? -1 : 1) * GetLength(vector) * .005;
				if (Math.Abs(df) < .01)
				{
					Map.CenterLocation = (Map.CenterLocation.ToPixel(Map.Zoom) + (PointD)vector).ToLocation(Map.Zoom);
					return;
				}
				Map.Zoom += df;

				var newCenterPix = Map.CenterLocation.ToPixel(Map.Zoom);
				var goalOriginPix = lockLoc.ToPixel(Map.Zoom);

				var paddedRect = Map.PaddedRect;
				var newMousePix = new PointD(newCenterPix.X + ((paddedRect.Width / 2) - lockPos.X) + paddedRect.Left, newCenterPix.Y + ((paddedRect.Height / 2) - lockPos.Y) + paddedRect.Top);
				Map.CenterLocation = (newCenterPix - (goalOriginPix - newMousePix)).ToLocation(Map.Zoom);
			}
		};
		Map.PointerReleased += (s, e) => StartPoints.Remove(e.Pointer);
		Map.PointerWheelChanged += (s, e) =>
		{
			if (config.Map.DisableManualMapControl || Map.IsNavigating)
				return;

			var mousePos = e.GetCurrentPoint(Map).Position;
			var mouseLoc = GetLocation(mousePos);

			var newZoom = Math.Clamp(Map.Zoom + e.Delta.Y * 0.25, Map.MinZoom, Map.MaxZoom);
			if (Math.Abs(newZoom - Map.Zoom) < .001)
				return;

			var newCenterPix = Map.CenterLocation.ToPixel(newZoom);
			var goalMousePix = mouseLoc.ToPixel(newZoom);

			var paddedRect = Map.PaddedRect;
			var newMousePix = new PointD(newCenterPix.X + ((paddedRect.Width / 2) - mousePos.X) + paddedRect.Left, newCenterPix.Y + ((paddedRect.Height / 2) - mousePos.Y) + paddedRect.Top);

			Map.Zoom = newZoom;
			Map.CenterLocation = (newCenterPix - (goalMousePix - newMousePix)).ToLocation(newZoom);
		};
		Map.DoubleTapped += (s, e) =>
		{
			if (config.Map.DisableManualMapControl || Map.IsNavigating)
				return;

			var mousePos = e.GetPosition(Map);
			var mouseLoc = GetLocation(mousePos);

			var newZoom = Math.Clamp(Map.Zoom + 1, Map.MinZoom, Map.MaxZoom);
			if (Math.Abs(newZoom - Map.Zoom) < .001)
				return;

			var newCenterPix = Map.CenterLocation.ToPixel(newZoom);
			var goalMousePix = mouseLoc.ToPixel(newZoom);

			var paddedRect = Map.PaddedRect;
			var newMousePix = new PointD(newCenterPix.X + ((paddedRect.Width / 2) - mousePos.X) + paddedRect.Left, newCenterPix.Y + ((paddedRect.Height / 2) - mousePos.Y) + paddedRect.Top);

			var newCenterPixel = newCenterPix - (goalMousePix - newMousePix);
			Map.Navigate(new RectD(
				(newCenterPixel - paddedRect.Size / 2).ToLocation(newZoom).CastPoint(),
				(newCenterPixel + paddedRect.Size / 2).ToLocation(newZoom).CastPoint()
			), TimeSpan.FromSeconds(.2), true);
		};

		Map.Zoom = 6;
		Map.CenterLocation = new KyoshinMonitorLib.Location(36.474f, 135.264f);

		Map.WhenAnyValue(m => m.CenterLocation, m => m.Zoom).Sample(TimeSpan.FromSeconds(.1)).Subscribe(m =>
		{
			var config = Locator.Current.RequireService<KyoshinEewViewerConfiguration>();
			Dispatcher.UIThread.InvokeAsync(new Action(() =>
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
	}

	private Dictionary<IPointer, Point> StartPoints { get; } = new();

	private static double GetLength(Point p)
		=> Math.Sqrt(p.X * p.X + p.Y * p.Y);

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
