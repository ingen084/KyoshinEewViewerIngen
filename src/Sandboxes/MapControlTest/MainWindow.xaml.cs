using KyoshinEewViewer.MapControl;
using KyoshinEewViewer.MapControl.RenderObjects;
using KyoshinMonitorLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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
		protected override async void OnInitialized(EventArgs e)
		{
			base.OnInitialized(e);

			map.Zoom = 5;
			map.CenterLocation = new Location(36.474f, 135.264f);

			map.InitalizeAsync(await TopologyMap.LoadAsync(@"japan_map_m.mpk.lz4"));

			var obj = new List<RenderObject>
			{
				new EllipseRenderObject(new Location(39.563f, 135.615f), 500000, null, new Pen(new SolidColorBrush(Color.FromArgb(200, 0, 160, 255)), 1)),
				new EllipseRenderObject(new Location(39.563f, 135.615f), 300000, new RadialGradientBrush(new GradientStopCollection(new[] { new GradientStop(Color.FromArgb(0, 255, 80, 120), .6), new GradientStop(Color.FromArgb(80, 255, 80, 120), 1) })), new Pen(new SolidColorBrush(Color.FromArgb(200, 255, 80, 120)), 1)),
				new EewCenterRenderObject(new Location(39.563f, 135.615f)),
				new RawIntensityRenderObject(new Location(34.4312f, 135.2294f), 4),
			};
			map.RenderObjects = obj;
		}

		private void Grid_MouseWheel(object sender, MouseWheelEventArgs e)
		{

			var paddedRect = map.PaddedRect;
			var centerPix = map.CenterLocation.ToPixel(map.Zoom);
			var mousePos = e.GetPosition(map);
			var mousePix = new Point(centerPix.X + ((paddedRect.Width / 2) - mousePos.X) + paddedRect.Left, centerPix.Y + ((paddedRect.Height / 2) - mousePos.Y) + paddedRect.Top);
			var mouseLoc = mousePix.ToLocation(map.Zoom);

			map.Zoom += e.Delta / 120 * 0.25;

			var newCenterPix = map.CenterLocation.ToPixel(map.Zoom);
			var goalMousePix = mouseLoc.ToPixel(map.Zoom);

			var newMousePix = new Point(newCenterPix.X + ((paddedRect.Width / 2) - mousePos.X) + paddedRect.Left, newCenterPix.Y + ((paddedRect.Height / 2) - mousePos.Y) + paddedRect.Top);

			map.CenterLocation = (map.CenterLocation.ToPixel(map.Zoom) - (goalMousePix - newMousePix)).ToLocation(map.Zoom);
		}

		Point _prevPos;
		private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
		{
			_prevPos = Mouse.GetPosition(map);
		}
		private void Grid_MouseMove(object sender, MouseEventArgs e)
		{
			if (Mouse.LeftButton == MouseButtonState.Pressed)
			{
				var curPos = Mouse.GetPosition(map);
				var diff = _prevPos - curPos;
				_prevPos = curPos;
				map.CenterLocation = (map.CenterLocation.ToPixel(map.Zoom) + diff).ToLocation(map.Zoom);
			}

			var rect = map.PaddedRect;

			var centerPos = map.CenterLocation.ToPixel(map.Zoom);
			var mousePos = e.GetPosition(map);
			var mouseLoc = new Point(centerPos.X + ((rect.Width / 2) - mousePos.X) + rect.Left, centerPos.Y + ((rect.Height / 2) - mousePos.Y) + rect.Top).ToLocation(map.Zoom);

			mousePosition.Text = $"Mouse Lat: {mouseLoc.Latitude.ToString("0.000000")} / Lng: {mouseLoc.Longitude.ToString("0.000000")}";
		}
	}
}
