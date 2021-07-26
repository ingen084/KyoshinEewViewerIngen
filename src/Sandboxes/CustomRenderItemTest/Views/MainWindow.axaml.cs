using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using KyoshinEewViewer.Map;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Reactive.Linq;

namespace CustomRenderItemTest.Views
{
	public class MainWindow : Window
	{
		private Point _prevPos;

		public MainWindow()
		{
			InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);

			var map = this.FindControl<MapControl>("map");
			App.Selector?.WhenAnyValue(x => x.SelectedWindowTheme).Where(x => x != null)
					.Subscribe(x => map.RefleshResourceCache());
			map.PointerMoved += (s, e2) =>
			{
				//if (mapControl1.IsNavigating)
				//	return;
				var pointer = e2.GetCurrentPoint(map);
				var curPos = pointer.Position;
				if (pointer.Properties.IsLeftButtonPressed)
				{
					var diff = new PointD(_prevPos.X - curPos.X, _prevPos.Y - curPos.Y);
					map.CenterLocation = (map.CenterLocation.ToPixel(map.Projection, map.Zoom) + diff).ToLocation(map.Projection, map.Zoom);
				}

				_prevPos = curPos;
				//var rect = map.PaddedRect;

				//var centerPos = map.CenterLocation.ToPixel(map.Projection, map.Zoom);
				//var mouseLoc = new PointD(centerPos.X + ((rect.Width / 2) - curPos.X) + rect.Left, centerPos.Y + ((rect.Height / 2) - curPos.Y) + rect.Top).ToLocation(mapControl1.Projection, mapControl1.Zoom);

				//label1.Text = $"Mouse Lat: {mouseLoc.Latitude:0.000000} / Lng: {mouseLoc.Longitude:0.000000}";
			};
			map.PointerPressed += (s, e2) =>
			{
				var pointer = e2.GetCurrentPoint(map);
				if (pointer.Properties.IsLeftButtonPressed)
					_prevPos = pointer.Position;
			};
			map.PointerWheelChanged += (s, e) =>
			{
				var pointer = e.GetCurrentPoint(map);
				var paddedRect = map.PaddedRect;
				var centerPix = map.CenterLocation.ToPixel(map.Projection, map.Zoom);
				var mousePos = pointer.Position;
				var mousePix = new PointD(centerPix.X + ((paddedRect.Width / 2) - mousePos.X) + paddedRect.Left, centerPix.Y + ((paddedRect.Height / 2) - mousePos.Y) + paddedRect.Top);
				var mouseLoc = mousePix.ToLocation(map.Projection, map.Zoom);

				map.Zoom += e.Delta.Y * 0.25;

				var newCenterPix = map.CenterLocation.ToPixel(map.Projection, map.Zoom);
				var goalMousePix = mouseLoc.ToPixel(map.Projection, map.Zoom);

				var newMousePix = new PointD(newCenterPix.X + ((paddedRect.Width / 2) - mousePos.X) + paddedRect.Left, newCenterPix.Y + ((paddedRect.Height / 2) - mousePos.Y) + paddedRect.Top);

				map.CenterLocation = (map.CenterLocation.ToPixel(map.Projection, map.Zoom) - (goalMousePix - newMousePix)).ToLocation(map.Projection, map.Zoom);
			};

			map.Zoom = 6;
			map.CenterLocation = new KyoshinMonitorLib.Location(36.474f, 135.264f);

			map.CustomColorMap = new Dictionary<int, Color>();
			var random = new Random();
			foreach(var p in map.Map[LandLayerType.PrefectureForecastArea].Polygons ?? Array.Empty<TopologyPolygon>())
			{
				if (p.Code is not int c)
					return;
				//map.CustomColorMap[c] = Color.FromUInt32((uint)random.Next(int.MinValue, int.MaxValue));
			}
		}
	}
}
