using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using KyoshinEewViewer.Map;

namespace KyoshinEewViewer.Views
{
	public class MainWindow : Window
	{
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

			map = this.FindControl<MapControl>("map");
			map.PointerMoved += (s, e2) =>
			{
				//if (mapControl1.IsNavigating)
				//	return;
				var pointer = e2.GetCurrentPoint(this);
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
				var pointer = e2.GetCurrentPoint(this);
				if (pointer.Properties.IsLeftButtonPressed)
					_prevPos = pointer.Position;
				if (pointer.Properties.IsRightButtonPressed)
					map.Navigate(new RectD(new PointD(23.996627, 123.469848), new PointD(24.662051, 124.420166)));
				if (pointer.Properties.IsMiddleButtonPressed)
					map.Navigate(new RectD(new PointD(24.058240, 123.046875), new PointD(45.706479, 146.293945)));
			};
			map.PointerWheelChanged += (s, e) =>
			{
				var pointer = e.GetCurrentPoint(this);
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

			//map.Map = TopologyMap.LoadCollection(Properties.Resources.world_mpk);
			map.Zoom = 6;
			map.CenterLocation = new KyoshinMonitorLib.Location(36.474f, 135.264f);
		}

		MapControl map;
		Point _prevPos;
	}
}