using Avalonia;
using Avalonia.Controls;
using FluentAvalonia.Core.ApplicationModel;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Map.Data;
using KyoshinEewViewer.Map.Layers;
using KyoshinEewViewer.Map;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Input;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;

namespace CustomRenderItemTest.Views;
public partial class MainView : UserControl
{
	public MainView()
	{
		InitializeComponent();

		listMode.Items = Enum.GetValues(typeof(RealtimeDataRenderMode));
		listMode.SelectedIndex = 0;

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
			StartPoints.Add(e.Pointer, originPos);
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

			if (StartPoints.Count <= 1)
				map.CenterLocation = (map.CenterLocation.ToPixel(map.Zoom) + (PointD)vector).ToLocation(map.Zoom);
			else
			{
				var lockPos = StartPoints.First(p => p.Key != e.Pointer).Value;

				var befLen = GetLength(lockPos - beforePoint);
				var newLen = GetLength(lockPos - newPosition);
				var lockLoc = GetLocation(lockPos);

				var df = (befLen > newLen ? -1 : 1) * GetLength(vector) * .01;
				if (Math.Abs(df) < .02)
				{
					map.CenterLocation = (map.CenterLocation.ToPixel(map.Zoom) + (PointD)vector).ToLocation(map.Zoom);
					return;
				}
				map.Zoom += df;
				Debug.WriteLine("複数移動 " + df);

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
			var mousePos = e.GetCurrentPoint(map).Position;
			var mouseLoc = GetLocation(mousePos);

			var newZoom = Math.Clamp(map.Zoom + e.Delta.Y * 0.25, map.MinZoom, map.MaxZoom);

			var newCenterPix = map.CenterLocation.ToPixel(newZoom);
			var goalMousePix = mouseLoc.ToPixel(newZoom);

			var paddedRect = map.PaddedRect;
			var newMousePix = new PointD(newCenterPix.X + ((paddedRect.Width / 2) - mousePos.X) + paddedRect.Left, newCenterPix.Y + ((paddedRect.Height / 2) - mousePos.Y) + paddedRect.Top);

			map.Zoom = newZoom;
			map.CenterLocation = (newCenterPix - (goalMousePix - newMousePix)).ToLocation(newZoom);
		};

		map.Zoom = 6;
		map.CenterLocation = new KyoshinMonitorLib.Location(36.474f, 135.264f);

		Task.Run(async () =>
		{
			var mapData = await MapData.LoadDefaultMapAsync();
			var landLayer = new LandLayer { Map = mapData };
			var landBorderLayer = new LandBorderLayer { Map = mapData };
			map.Layers = new MapLayer[] {
				landLayer,
				landBorderLayer,
				new GridLayer(),
			};
		});
	}

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnAttachedToVisualTree(e);

		// Changed for SplashScreens:
		// -- If using a SplashScreen, the window will be available when this is attached
		//    and we can just call OnParentWindowOpened
		// -- If not using a SplashScreen (like before), the window won't be initialized
		//    yet and setting our custom titlebar won't work... so wait for the 
		//    WindowOpened event first
		if (e.Root is Window b)
		{
			if (!b.IsActive)
				b.Opened += OnParentWindowOpened;
			else
				OnParentWindowOpened(b, null);
		}

		//_windowIconControl = this.FindControl<IControl>("WindowIcon");
		//_frameView = this.FindControl<Frame>("FrameView");
		//_navView = this.FindControl<NavigationView>("NavView");
		//_navView.MenuItems = GetNavigationViewItems();
		//_navView.FooterMenuItems = GetFooterNavigationViewItems();

		//_frameView.Navigated += OnFrameViewNavigated;
		//_navView.ItemInvoked += OnNavigationViewItemInvoked;
		//_navView.BackRequested += OnNavigationViewBackRequested;

		//_frameView.Navigate(typeof(HomePage));

		//NavigationService.Instance.SetFrame(_frameView);
		//NavigationService.Instance.SetOverlayHost(this.FindControl<Panel>("OverlayHost"));
	}

	private void OnParentWindowOpened(object? sender, EventArgs? e)
	{
		if (e != null && sender is Window w)
			w.Opened -= OnParentWindowOpened;

		if (sender is CoreWindow cw)
		{
			var titleBar = cw.TitleBar;
			if (titleBar != null)
			{
				titleBar.ExtendViewIntoTitleBar = true;

				titleBar.LayoutMetricsChanged += OnApplicationTitleBarLayoutMetricsChanged;

				cw.SetTitleBar(TitleBarHost);
				TitleBarHost.Margin = new Thickness(0, 0, titleBar.SystemOverlayRightInset, 0);
			}
		}
	}

	private void OnApplicationTitleBarLayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
		=> TitleBarHost.Margin = new Thickness(0, 0, sender.SystemOverlayRightInset, 0);

	private Dictionary<IPointer, Point> StartPoints { get; } = new();

	double GetLength(Point p)
		=> Math.Sqrt(p.X * p.X + p.Y * p.Y);
}
