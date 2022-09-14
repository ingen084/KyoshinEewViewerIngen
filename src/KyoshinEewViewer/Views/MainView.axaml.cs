using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Services;
using ReactiveUI;
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

		ConfigurationService.Current.Map.WhenAnyValue(x => x.DisableManualMapControl).Subscribe(x =>
		{
			homeButton.IsVisible = !x;
			//homeButton2.IsVisible = !x;
		});
		homeButton.IsVisible = !ConfigurationService.Current.Map.DisableManualMapControl;
		//homeButton2.IsVisible = !ConfigurationService.Current.Map.DisableManualMapControl;

		// �}�b�v�܂��̃n���h��
		App.Selector?.WhenAnyValue(x => x.SelectedWindowTheme).Where(x => x != null)
				.Subscribe(x => map.RefreshResourceCache());
		KyoshinMonitorLib.Location GetLocation(Point p)
		{
			var centerPix = map!.CenterLocation.ToPixel(map.Zoom);
			var originPix = new PointD(centerPix.X + ((map.PaddedRect.Width / 2) - p.X) + map.PaddedRect.Left, centerPix.Y + ((map.PaddedRect.Height / 2) - p.Y) + map.PaddedRect.Top);
			return originPix.ToLocation(map.Zoom);
		}
		map.PointerPressed += (s, e) =>
		{
			var originPos = e.GetCurrentPoint(map).Position;
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
		map.PointerMoved += (s, e) =>
		{
			if (!StartPoints.ContainsKey(e.Pointer))
				return;
			var newPosition = e.GetCurrentPoint(map).Position;
			var beforePoint = StartPoints[e.Pointer];
			var vector = beforePoint - newPosition;
			if (vector.IsDefault)
				return;
			StartPoints[e.Pointer] = newPosition;

			if (ConfigurationService.Current.Map.DisableManualMapControl || map.IsNavigating)
				return;

			if (StartPoints.Count <= 1)
				map.CenterLocation = (map.CenterLocation.ToPixel(map.Zoom) + (PointD)vector).ToLocation(map.Zoom);
			else
			{
				var lockPos = StartPoints.First(p => p.Key != e.Pointer).Value;

				var befLen = GetLength(lockPos - beforePoint);
				var newLen = GetLength(lockPos - newPosition);
				var lockLoc = GetLocation(lockPos);

				var df = (befLen > newLen ? -1 : 1) * GetLength(vector) * .005;
				if (Math.Abs(df) < .01)
				{
					map.CenterLocation = (map.CenterLocation.ToPixel(map.Zoom) + (PointD)vector).ToLocation(map.Zoom);
					return;
				}
				map.Zoom += df;

				var newCenterPix = map.CenterLocation.ToPixel(map.Zoom);
				var goalOriginPix = lockLoc.ToPixel(map.Zoom);

				var paddedRect = map.PaddedRect;
				var newMousePix = new PointD(newCenterPix.X + ((paddedRect.Width / 2) - lockPos.X) + paddedRect.Left, newCenterPix.Y + ((paddedRect.Height / 2) - lockPos.Y) + paddedRect.Top);
				map.CenterLocation = (newCenterPix - (goalOriginPix - newMousePix)).ToLocation(map.Zoom);
			}
		};
		map.PointerReleased += (s, e) => StartPoints.Remove(e.Pointer);
		map.PointerWheelChanged += (s, e) =>
		{
			if (ConfigurationService.Current.Map.DisableManualMapControl || map.IsNavigating)
				return;

			var mousePos = e.GetCurrentPoint(map).Position;
			var mouseLoc = GetLocation(mousePos);

			var newZoom = Math.Clamp(map.Zoom + e.Delta.Y * 0.25, map.MinZoom, map.MaxZoom);
			if (newZoom == map.Zoom)
				return;

			var newCenterPix = map.CenterLocation.ToPixel(newZoom);
			var goalMousePix = mouseLoc.ToPixel(newZoom);

			var paddedRect = map.PaddedRect;
			var newMousePix = new PointD(newCenterPix.X + ((paddedRect.Width / 2) - mousePos.X) + paddedRect.Left, newCenterPix.Y + ((paddedRect.Height / 2) - mousePos.Y) + paddedRect.Top);

			map.Zoom = newZoom;
			map.CenterLocation = (newCenterPix - (goalMousePix - newMousePix)).ToLocation(newZoom);
		};
		map.DoubleTapped += (s, e) =>
		{
			if (ConfigurationService.Current.Map.DisableManualMapControl || map.IsNavigating)
				return;

			var mousePos = e.GetPosition(map);
			var mouseLoc = GetLocation(mousePos);

			var newZoom = Math.Clamp(map.Zoom + 1, map.MinZoom, map.MaxZoom);
			if (newZoom == map.Zoom)
				return;

			var newCenterPix = map.CenterLocation.ToPixel(newZoom);
			var goalMousePix = mouseLoc.ToPixel(newZoom);

			var paddedRect = map.PaddedRect;
			var newMousePix = new PointD(newCenterPix.X + ((paddedRect.Width / 2) - mousePos.X) + paddedRect.Left, newCenterPix.Y + ((paddedRect.Height / 2) - mousePos.Y) + paddedRect.Top);

			var newCenterPixel = newCenterPix - (goalMousePix - newMousePix);
			map.Navigate(new RectD(
				(newCenterPixel - paddedRect.Size / 2).ToLocation(newZoom).CastPoint(),
				(newCenterPixel + paddedRect.Size / 2).ToLocation(newZoom).CastPoint()
			), TimeSpan.FromSeconds(.2), true);
		};

		map.Zoom = 6;
		map.CenterLocation = new KyoshinMonitorLib.Location(36.474f, 135.264f);

		//updateButton.Click += (s, e) => SubWindowsService.Default.ShowUpdateWindow();
		//updateButton2.Click += (s, e) => SubWindowsService.Default.ShowUpdateWindow();

		//this.WhenAnyValue(x => x.DataContext)
		//	.Subscribe(c => (c as MainWindowViewModel)?.WhenAnyValue(x => x.Scale).Subscribe(s => InvalidateMeasure()));
		//this.WhenAnyValue(x => x.Bounds).Subscribe(x => InvalidateMeasure());

		MessageBus.Current.Listen<MapNavigationRequested>().Subscribe(x =>
		{
			if (!ConfigurationService.Current.Map.AutoFocus)
				return;
			if (x.Bound is Rect rect)
			{
				if (x.MustBound is Rect mustBound)
					map.Navigate(rect, ConfigurationService.Current.Map.AutoFocusAnimation ? TimeSpan.FromSeconds(.3) : TimeSpan.Zero, mustBound);
				else
					map.Navigate(rect, ConfigurationService.Current.Map.AutoFocusAnimation ? TimeSpan.FromSeconds(.3) : TimeSpan.Zero);
			}
			else
				NavigateToHome();
		});
		MessageBus.Current.Listen<RegistMapPositionRequested>().Subscribe(x =>
		{
			var halfPaddedRect = new PointD(map.PaddedRect.Width / 2, -map.PaddedRect.Height / 2);
			var centerPixel = map.CenterLocation.ToPixel(map.Zoom);

			ConfigurationService.Current.Map.Location1 = (centerPixel + halfPaddedRect).ToLocation(map.Zoom);
			ConfigurationService.Current.Map.Location2 = (centerPixel - halfPaddedRect).ToLocation(map.Zoom);
		});
	}

	private Dictionary<IPointer, Point> StartPoints { get; } = new();

	double GetLength(Point p)
		=> Math.Sqrt(p.X * p.X + p.Y * p.Y);

	private void NavigateToHome()
		=> map?.Navigate(
			new RectD(ConfigurationService.Current.Map.Location1.CastPoint(), ConfigurationService.Current.Map.Location2.CastPoint()),
			ConfigurationService.Current.Map.AutoFocusAnimation ? TimeSpan.FromSeconds(.3) : TimeSpan.Zero);
}
