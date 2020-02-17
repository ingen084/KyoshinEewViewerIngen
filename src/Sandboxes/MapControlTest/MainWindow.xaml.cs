using KyoshinEewViewer.MapControl;
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
        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            map.Zoom = 5;
            map.CenterLocation = new Location(36.474f, 135.264f);
        }

        private void Grid_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var centerPix = map.CenterLocation.ToPixel(map.Zoom);
            var mousePos = e.GetPosition(map);
            var mousePix = new Point(centerPix.X + ((map.RenderSize.Width / 2) - mousePos.X), centerPix.Y + ((map.RenderSize.Height / 2) - mousePos.Y));
            var mouseLoc = mousePix.ToLocation(map.Zoom);

            map.Zoom += e.Delta / 120 * 0.25;

            var newCenterPix = map.CenterLocation.ToPixel(map.Zoom);
            var goalMousePix = mouseLoc.ToPixel(map.Zoom);

            var newMousePix = new Point(newCenterPix.X + ((map.RenderSize.Width / 2) - mousePos.X), newCenterPix.Y + ((map.RenderSize.Height / 2) - mousePos.Y));

            map.CenterLocation = (map.CenterLocation.ToPixel(map.Zoom) - (goalMousePix - newMousePix)).ToLocation(map.Zoom);
        }

        private void Grid_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
        {
            var centerPix = map.CenterLocation.ToPixel(map.Zoom);
            var mousePos = e.ManipulationOrigin;
            var mousePix = new Point(centerPix.X + ((map.RenderSize.Width / 2) - mousePos.X), centerPix.Y + ((map.RenderSize.Height / 2) - mousePos.Y));
            var mouseLoc = mousePix.ToLocation(map.Zoom);

            map.Zoom += e.DeltaManipulation.Scale.X / 120;

            var newCenterPix = map.CenterLocation.ToPixel(map.Zoom);
            var goalMousePix = mouseLoc.ToPixel(map.Zoom);

            var newMousePix = new Point(newCenterPix.X + ((map.RenderSize.Width / 2) - mousePos.X), newCenterPix.Y + ((map.RenderSize.Height / 2) - mousePos.Y));

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

            var centerPos = map.CenterLocation.ToPixel(map.Zoom);
            var mousePos = e.GetPosition(map);
            var mouseLoc = new Point(centerPos.X + ((map.RenderSize.Width / 2) - mousePos.X), centerPos.Y + ((map.RenderSize.Height / 2) - mousePos.Y)).ToLocation(map.Zoom);

            mousePosition.Text = $"Mouse Lat: {mouseLoc.Latitude.ToString("0.000000")} / Lng: {mouseLoc.Longitude.ToString("0.000000")}";
        }
    }
}
