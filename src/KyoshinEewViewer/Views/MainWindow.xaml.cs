using KyoshinEewViewer.MapControl;
using KyoshinMonitorLib;
using System.Windows;
using System.Windows.Input;

namespace KyoshinEewViewer.Views
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private bool isFullScreen;
		public MainWindow()
		{
			InitializeComponent();

			KeyDown += (s, e) =>
			{
				if (e.Key != Key.F11)
					return;

				if (isFullScreen)
				{
					WindowStyle = WindowStyle.SingleBorderWindow;
					WindowState = WindowState.Normal;
					isFullScreen = false;
					return;
				}
				WindowStyle = WindowStyle.None;
				WindowState = WindowState.Maximized;
				isFullScreen = true;
			};
		}

		private void Map_MouseWheel(object sender, MouseWheelEventArgs e)
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
		private void Map_MouseDown(object sender, MouseButtonEventArgs e)
		{
			_prevPos = Mouse.GetPosition(map);
		}
		private void Map_MouseMove(object sender, MouseEventArgs e)
		{
			if (Mouse.LeftButton == MouseButtonState.Pressed)
			{
				var curPos = Mouse.GetPosition(map);
				var diff = _prevPos - curPos;
				_prevPos = curPos;
				map.CenterLocation = (map.CenterLocation.ToPixel(map.Zoom) + diff).ToLocation(map.Zoom);
			}
		}
	}
}