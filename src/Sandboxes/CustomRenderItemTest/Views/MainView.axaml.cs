using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Data;
using KyoshinEewViewer.Map.Layers;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace CustomRenderItemTest.Views;
public partial class MainView : UserControl
{
	public MainView()
	{
		InitializeComponent();

		ListMode.ItemsSource = Enum.GetValues(typeof(RealtimeDataRenderMode));
		ListMode.SelectedIndex = 0;

		App.Selector?.WhenAnyValue(x => x.SelectedWindowTheme).Where(x => x != null)
				.Subscribe(x => Map.RefreshResourceCache());
		KyoshinMonitorLib.Location GetLocation(Point p)
		{
			var centerPix = Map.CenterLocation.ToPixel(Map.Zoom);
			var originPix = new PointD(centerPix.X + ((Map.PaddedRect.Width / 2) - p.X) + Map.PaddedRect.Left, centerPix.Y + ((Map.PaddedRect.Height / 2) - p.Y) + Map.PaddedRect.Top);
			return originPix.ToLocation(Map.Zoom);
		}
		Map.PointerPressed += (s, e) =>
		{
			var originPos = e.GetCurrentPoint(Map).Position;
			StartPoints.Add(e.Pointer, originPos);
			// 3点以上の場合は2点になるようにする
			if (StartPoints.Count <= 2)
				return;
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
			if (vector is { X: 0, Y: 0 })
				return;
			StartPoints[e.Pointer] = newPosition;

			if (StartPoints.Count <= 1)
				Map.CenterLocation = (Map.CenterLocation.ToPixel(Map.Zoom) + (PointD)vector).ToLocation(Map.Zoom);
			else
			{
				var lockPos = StartPoints.First(p => p.Key != e.Pointer).Value;

				var befLen = GetLength(lockPos - beforePoint);
				var newLen = GetLength(lockPos - newPosition);
				var lockLoc = GetLocation(lockPos);

				var df = (befLen > newLen ? -1 : 1) * GetLength(vector) * .01;
				if (Math.Abs(df) < .02)
				{
					Map.CenterLocation = (Map.CenterLocation.ToPixel(Map.Zoom) + (PointD)vector).ToLocation(Map.Zoom);
					return;
				}
				Map.Zoom += df;
				Debug.WriteLine("複数移動 " + df);

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
			var mousePos = e.GetCurrentPoint(Map).Position;
			var mouseLoc = GetLocation(mousePos);

			var newZoom = Math.Clamp(Map.Zoom + e.Delta.Y * 0.25, Map.MinZoom, Map.MaxZoom);

			var newCenterPix = Map.CenterLocation.ToPixel(newZoom);
			var goalMousePix = mouseLoc.ToPixel(newZoom);

			var paddedRect = Map.PaddedRect;
			var newMousePix = new PointD(newCenterPix.X + ((paddedRect.Width / 2) - mousePos.X) + paddedRect.Left, newCenterPix.Y + ((paddedRect.Height / 2) - mousePos.Y) + paddedRect.Top);

			Map.Zoom = newZoom;
			Map.CenterLocation = (newCenterPix - (goalMousePix - newMousePix)).ToLocation(newZoom);
		};

		Map.Zoom = 6;
		Map.CenterLocation = new KyoshinMonitorLib.Location(36.474f, 135.264f);

		Task.Run(async () =>
		{
			var mapData = await MapData.LoadDefaultMapAsync();
			var landLayer = new LandLayer { Map = mapData };
			var landBorderLayer = new LandBorderLayer { Map = mapData };
			Map.Layers = new MapLayer[] {
				landLayer,
				landBorderLayer,
				new GridLayer(),
			};
		});
	}

	private Dictionary<IPointer, Point> StartPoints { get; } = new();

	private static double GetLength(Point p)
		=> Math.Sqrt(p.X * p.X + p.Y * p.Y);
}
