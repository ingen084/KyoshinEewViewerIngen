using KyoshinEewViewer.MapControl;
using KyoshinMonitorLib;
using MessagePack;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace LightningMapTest
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
		List<LightningRealtimeRenderObject> lightningCache = new List<LightningRealtimeRenderObject>();
		TimeSpan DeleteTime = TimeSpan.FromSeconds(20);

		protected override void OnInitialized(EventArgs e)
		{
			base.OnInitialized(e);

			map.Map = MessagePackSerializer.Deserialize<TopologyMap>(Properties.Resources.world_mpk, MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));
			map.Zoom = 5;
			map.CenterLocation = new Location(36.474f, 135.264f);

			var conn = new LightningMapConnection();
			conn.Arrived += e =>
			{
				lightningCache.Insert(0, new LightningRealtimeRenderObject(DateTimeOffset.FromUnixTimeMilliseconds(e.time / 1000000).LocalDateTime, DateTime.Now, new Location(e.lat, e.lon)));
				lightningCache.RemoveAll(l => l.TimeOffset >= DeleteTime);
				Dispatcher.Invoke(() => map.RealtimeRenderObjects = lightningCache.ToArray());
			};
			conn.Connect();
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			base.OnClosing(e);
		}

		private void Grid_MouseWheel(object sender, MouseWheelEventArgs e)
		{
			if (map.IsNavigating)
				return;
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
			//if (e.RightButton == MouseButtonState.Pressed)
			//	map.Navigate(new Rect(new Point(23.996627, 123.469848), new Point(24.662051, 124.420166)), new Duration(TimeSpan.FromSeconds(.5)));
			//if (e.MiddleButton == MouseButtonState.Pressed)
			//	map.Navigate(new Rect(new Point(24.058240, 123.046875), new Point(45.706479, 146.293945)), new Duration(TimeSpan.FromSeconds(.5)));
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

			mousePosition.Text = $"Mouse Lat: {mouseLoc.Latitude:0.000000} / Lng: {mouseLoc.Longitude:0.000000}";

			_prevPos = Mouse.GetPosition(map);
		}
	}
}
