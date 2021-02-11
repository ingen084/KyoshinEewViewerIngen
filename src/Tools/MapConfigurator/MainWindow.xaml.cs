using KyoshinEewViewer.MapControl;
using KyoshinMonitorLib;
using MessagePack;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
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

namespace MapConfigurator
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();

			map.Zoom = 5;
			map.CenterLocation = new Location(36.474f, 135.264f);

			loadMenuItem.Click += (s, e) =>
			{
				var dialog = new OpenFileDialog
				{
					Filter = "MessagePack(LZ4)|*.mpk.lz4",
					FilterIndex = 1,
					RestoreDirectory = true
				};
				if (dialog.ShowDialog() == true)
				{
					using var stream = File.OpenRead(dialog.FileName);
					map.Map = MessagePackSerializer.Deserialize<Dictionary<LandLayerType, TopologyMap>>(stream, MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));
				}
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
		}
		private void Grid_MouseMove(object sender, MouseEventArgs e)
		{
			var curPos = Mouse.GetPosition(map);
			if (map.IsNavigating)
				return;
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

			mousePosition.Text = $"Mouse Lat: {mouseLoc.Latitude:0.0000} / Lng: {mouseLoc.Longitude:0.0000}";
		}
	}
}
