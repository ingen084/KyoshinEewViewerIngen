using KyoshinEewViewer.MapControl;
using KyoshinEewViewer.MapControl.Projections;
using KyoshinMonitorLib;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace MapControlTest
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
		}
		protected override void OnInitialized(EventArgs e)
		{
			base.OnInitialized(e);

			var sw = Stopwatch.StartNew();
			var mm = TopologyMap.Load(@"world.mpk.lz4");
			sw.Stop();
			Debug.WriteLine(sw.ElapsedMilliseconds + "ms");
			map.Map = mm;
			map.Zoom = 5;
			map.CenterLocation = new Location(36.474f, 135.264f);


			//var obj = new List<RenderObject>
			//{
			//	new EllipseRenderObject(new Location(39.563f, 135.615f), 500000, null, new Pen(new SolidColorBrush(Color.FromArgb(200, 0, 160, 255)), 1)),
			//	new EllipseRenderObject(new Location(39.563f, 135.615f), 300000, new RadialGradientBrush(new GradientStopCollection(new[] { new GradientStop(Color.FromArgb(0, 255, 80, 120), .6), new GradientStop(Color.FromArgb(80, 255, 80, 120), 1) })), new Pen(new SolidColorBrush(Color.FromArgb(200, 255, 80, 120)), 1)),
			//	new EewCenterRenderObject(new Location(39.563f, 135.615f)),
			//	new RawIntensityRenderObject(new Location(34.4312f, 135.2294f), "test point", 4),
			//};
			//map.RenderObjects = obj.ToArray();
			rateSlider.ValueChanged += (s, e) => 
			{
				if (map.Projection is not MillerProjection mp)
					map.Projection = mp = new MillerProjection();
				mp.Rate = (float)e.NewValue;
				map.ClearFeatureCache();
			};
		}

		private void Grid_MouseWheel(object sender, MouseWheelEventArgs e)
		{
			if (map.IsNavigating)
				return;
			var paddedRect = map.PaddedRect;
			var centerPix = map.CenterLocation.ToPixel(map.Projection, map.Zoom);
			var mousePos = e.GetPosition(map);
			var mousePix = new Point(centerPix.X + ((paddedRect.Width / 2) - mousePos.X) + paddedRect.Left, centerPix.Y + ((paddedRect.Height / 2) - mousePos.Y) + paddedRect.Top);
			var mouseLoc = mousePix.ToLocation(map.Projection, map.Zoom);

			map.Zoom += e.Delta / 120 * 0.25;

			var newCenterPix = map.CenterLocation.ToPixel(map.Projection, map.Zoom);
			var goalMousePix = mouseLoc.ToPixel(map.Projection, map.Zoom);

			var newMousePix = new Point(newCenterPix.X + ((paddedRect.Width / 2) - mousePos.X) + paddedRect.Left, newCenterPix.Y + ((paddedRect.Height / 2) - mousePos.Y) + paddedRect.Top);

			map.CenterLocation = (map.CenterLocation.ToPixel(map.Projection, map.Zoom) - (goalMousePix - newMousePix)).ToLocation(map.Projection, map.Zoom);
		}

		Point _prevPos;
		private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (e.LeftButton == MouseButtonState.Pressed)
				_prevPos = Mouse.GetPosition(map);
			if (e.RightButton == MouseButtonState.Pressed)
				map.Navigate(new Rect(new Point(23.996627, 123.469848), new Point(24.662051, 124.420166)), new Duration(TimeSpan.FromSeconds(.5)));
			if (e.MiddleButton == MouseButtonState.Pressed)
				map.Navigate(new Rect(new Point(24.058240, 123.046875), new Point(45.706479, 146.293945)), new Duration(TimeSpan.FromSeconds(.5)));
		}
		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
		{
			base.OnRenderSizeChanged(sizeInfo);
			//map.Navigate(new Rect(new Point(24.058240, 123.046875), new Point(45.706479, 146.293945)), new Duration(TimeSpan.Zero));
		}
		private void Grid_MouseMove(object sender, MouseEventArgs e)
		{
			if (map.IsNavigating)
				return;
			var curPos = Mouse.GetPosition(map);
			if (Mouse.LeftButton == MouseButtonState.Pressed)
			{
				var diff = _prevPos - curPos;
				map.CenterLocation = (map.CenterLocation.ToPixel(map.Projection, map.Zoom) + diff).ToLocation(map.Projection, map.Zoom);
			}

			_prevPos = curPos;
			var rect = map.PaddedRect;

			var centerPos = map.CenterLocation.ToPixel(map.Projection, map.Zoom);
			var mousePos = e.GetPosition(map);
			var mouseLoc = new Point(centerPos.X + ((rect.Width / 2) - mousePos.X) + rect.Left, centerPos.Y + ((rect.Height / 2) - mousePos.Y) + rect.Top).ToLocation(map.Projection, map.Zoom);

			mousePosition.Text = $"Mouse Lat: {mouseLoc.Latitude:0.000000} / Lng: {mouseLoc.Longitude:0.000000}";
		}
	}
}
