using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
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

public class MainWindow : Window
{
	private Dictionary<IPointer, Point> StartPoints { get; } = new();

	public MainWindow()
	{
		InitializeComponent();
#if DEBUG
		this.AttachDevTools();
#endif
	}

	double GetLength(Point p)
		=> Math.Sqrt(p.X * p.X + p.Y * p.Y);

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);


		var listMode = this.FindControl<ComboBox>("listMode")!;
		listMode.Items = Enum.GetValues(typeof(RealtimeDataRenderMode));
		listMode.SelectedIndex = 0;

		var map = this.FindControl<MapControl>("map")!;
		App.Selector?.WhenAnyValue(x => x.SelectedWindowTheme).Where(x => x != null)
				.Subscribe(x => map.RefreshResourceCache());
		map.PointerPressed += (s, e) =>
		{
			Debug.WriteLine($"{DateTime.Now.ToLongTimeString()} pressed {e.Pointer.GetHashCode()}");
			StartPoints.Add(e.Pointer, e.GetCurrentPoint(this).Position);
		};
		map.PointerMoved += (s, e) =>
		{
			//Debug.WriteLine($"{DateTime.Now.ToLongTimeString()} moved {e.Pointer.GetHashCode()}");
			if (!StartPoints.ContainsKey(e.Pointer))
				return;
			var newPosition = e.GetCurrentPoint(this).Position;
			var beforePosition = StartPoints[e.Pointer];
			var vector = beforePosition - newPosition;
			if (vector.IsDefault)
				return;
			StartPoints[e.Pointer] = newPosition;
			if (StartPoints.Count <= 1)
				map.CenterLocation = (map.CenterLocation.ToPixel(map.Zoom) + (PointD)vector).ToLocation(map.Zoom);
			else
			{
				var paddedRect = map.PaddedRect;
				var centerPix = map.CenterLocation.ToPixel(map.Zoom);

				var originPos = StartPoints.First(p => p.Key != e.Pointer).Value;
				var originPix = new PointD(centerPix.X + ((paddedRect.Width / 2) - originPos.X) + paddedRect.Left, centerPix.Y + ((paddedRect.Height / 2) - originPos.Y) + paddedRect.Top);
				var originLoc = originPix.ToLocation(map.Zoom);

				var beforePix = new PointD(centerPix.X + ((paddedRect.Width / 2) - beforePosition.X) + paddedRect.Left, centerPix.Y + ((paddedRect.Height / 2) - beforePosition.Y) + paddedRect.Top);
				var afterPix = new PointD(centerPix.X + ((paddedRect.Width / 2) - newPosition.X) + paddedRect.Left, centerPix.Y + ((paddedRect.Height / 2) - newPosition.Y) + paddedRect.Top);

				var befLen = GetLength(originPos - beforePosition);
				var newLen = GetLength(originPos - newPosition);

				var df = (befLen > newLen ? -1 : 1) * GetLength(vector) * .01;
				map.Zoom += df;
				Debug.WriteLine("複数移動 " + df);

				var newCenterPix = map.CenterLocation.ToPixel(map.Zoom);
				var goalMousePix = originLoc.ToPixel(map.Zoom);

				var newMousePix = new PointD(newCenterPix.X + ((paddedRect.Width / 2) - originPos.X) + paddedRect.Left, newCenterPix.Y + ((paddedRect.Height / 2) - originPos.Y) + paddedRect.Top);

				map.CenterLocation = (map.CenterLocation.ToPixel(map.Zoom) - (goalMousePix - newMousePix)).ToLocation(map.Zoom);
			}
		};
		map.PointerReleased += (s, e) =>
		{
			Debug.WriteLine($"{DateTime.Now.ToLongTimeString()} released {e.Pointer.GetHashCode()}");
			StartPoints.Remove(e.Pointer);
		};
		map.PointerWheelChanged += (s, e) =>
		{
			var pointer = e.GetCurrentPoint(map);
			var paddedRect = map.PaddedRect;
			var centerPix = map.CenterLocation.ToPixel(map.Zoom);
			var mousePos = pointer.Position;
			var mousePix = new PointD(centerPix.X + ((paddedRect.Width / 2) - mousePos.X) + paddedRect.Left, centerPix.Y + ((paddedRect.Height / 2) - mousePos.Y) + paddedRect.Top);
			var mouseLoc = mousePix.ToLocation(map.Zoom);

			map.Zoom += e.Delta.Y * 0.25;

			var newCenterPix = map.CenterLocation.ToPixel(map.Zoom);
			var goalMousePix = mouseLoc.ToPixel(map.Zoom);

			var newMousePix = new PointD(newCenterPix.X + ((paddedRect.Width / 2) - mousePos.X) + paddedRect.Left, newCenterPix.Y + ((paddedRect.Height / 2) - mousePos.Y) + paddedRect.Top);

			map.CenterLocation = (map.CenterLocation.ToPixel(map.Zoom) - (goalMousePix - newMousePix)).ToLocation(map.Zoom);
		};

		map.Zoom = 6;
		map.CenterLocation = new KyoshinMonitorLib.Location(36.474f, 135.264f);

		//map.CustomColorMap = new();
		//map.CustomColorMap[LandLayerType.EarthquakeInformationSubdivisionArea] = new();
		//var random = new Random();
		//foreach (var p in map.Map[LandLayerType.EarthquakeInformationSubdivisionArea].Polygons ?? Array.Empty<TopologyPolygon>())
		//{
		//	if (p.Code is not int c)
		//		return;
		//	map.CustomColorMap[LandLayerType.EarthquakeInformationSubdivisionArea][c] = new SKColor((uint)random.Next(int.MinValue, int.MaxValue));
		//}

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
}
