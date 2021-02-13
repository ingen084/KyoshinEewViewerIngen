using EarthquakeRenderTest.RenderObjects;
using DmdataSharp;
using DmdataSharp.ApiResponses.Parameters;
using KyoshinEewViewer.MapControl;
using KyoshinMonitorLib;
using MessagePack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace EarthquakeRenderTest
{
	public static class XNodeExtensions
	{
		public static XElement SelectNode(this XElement element, string name)
			=> element.Elements().FirstOrDefault(e => e.Name.LocalName == name);
	}

	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		DmdataApiClient ApiClient { get; }

		public MainWindow()
		{
			InitializeComponent();

			AllowDrop = true;
			Drop += (s, e) =>
			{
				if (e.Data.GetData(DataFormats.FileDrop) is string[] files && File.Exists(files[0]) && files[0].EndsWith(".xml"))
				{
					var fs = File.OpenRead(files[0]);
					ProcessXml(fs);
				}
			};
			PreviewDragOver += (s, e) =>
			{
				if (e.Data.GetDataPresent(DataFormats.FileDrop, true))
					e.Effects = DragDropEffects.Copy;
				else
					e.Effects = DragDropEffects.None;
				e.Handled = true;
			};

			map.Map = TopologyMap.LoadCollection(Properties.Resources.WorldMap);
			map.Zoom = 5;
			map.CenterLocation = new Location(36.474f, 135.264f);

			ApiClient = new DmdataApiClient(Environment.GetEnvironmentVariable("DMDATA_APIKEY"));

			InitEq();
		}

		EarthquakeStationParameterResponse Stations { get; set; }

		private async void InitEq()
		{
			Stations = await ApiClient.GetEarthquakeStationParameterAsync();

			eqHistoryCombobox.SelectionChanged += async (s, e) =>
			{
				var tStream = await ApiClient.GetTelegramStreamAsync(eqHistoryCombobox.SelectedItem as string);
				ProcessXml(tStream);
			};

			var last = await ApiClient.GetTelegramListAsync(type: "VXSE53");
			foreach (var item in last.Items)
				eqHistoryCombobox.Items.Add(item.Key);
			eqHistoryCombobox.SelectedIndex = 0;

			//ca83c447c58a46297a748356f47045bd6c1a4e8d6d8236d7cfa3aa3a4ec89caeea34626b290fb2b9e76bd1af85227217
		}

		public async void ProcessXml(Stream stream)
		{
			var objs = new List<IRenderObject>();
			var zoomPoints = new List<Location>();

			XDocument document;
			XmlNamespaceManager nsManager;

			using (var reader = XmlReader.Create(stream, new XmlReaderSettings { Async = true }))
			{
				document = await XDocument.LoadAsync(reader, LoadOptions.None, CancellationToken.None);
				nsManager = new XmlNamespaceManager(reader.NameTable);
			}
			nsManager.AddNamespace("jmx", "http://xml.kishou.go.jp/jmaxml1/");
			nsManager.AddNamespace("eb", "http://xml.kishou.go.jp/jmaxml1/body/seismology1/");
			nsManager.AddNamespace("jmx_eb", "http://xml.kishou.go.jp/jmaxml1/elementBasis1/");

			var title = document.Root.XPathSelectElement("/jmx:Report/jmx:Control/jmx:Title", nsManager)?.Value;
			if (title != "震源・震度に関する情報")
			{
				SystemSounds.Beep.Play();
				stream.Dispose();
				return;
			}

			infoTitle.Text = title;

			var coordinate = document.Root.XPathSelectElement("/jmx:Report/eb:Body/eb:Earthquake/eb:Hypocenter/eb:Area/jmx_eb:Coordinate", nsManager)?.Value;
			var hypoCenter = new HypoCenterRenderObject(CoordinateConverter.GetLocation(coordinate), false);

			zoomPoints.Add(new Location(hypoCenter.Location.Latitude - .3f, hypoCenter.Location.Longitude - .3f));
			zoomPoints.Add(new Location(hypoCenter.Location.Latitude + .3f, hypoCenter.Location.Longitude + .3f));

			var depthNullable = CoordinateConverter.GetDepth(coordinate);
			if (depthNullable is not int depth)
				depthBlock.Visibility = Visibility.Collapsed;
			else if (depth <= 0)
			{
				depthBlock.Visibility = Visibility.Visible;
				depthShallow.Visibility = Visibility.Visible;
				depthDeep.Visibility = Visibility.Collapsed;
			}
			else
			{
				depthBlock.Visibility = Visibility.Visible;
				depthShallow.Visibility = Visibility.Collapsed;
				depthDeep.Visibility = Visibility.Visible;
				depthValue.Text = depth.ToString();
			}

			hypocenterName.Text = document.XPathSelectElement("/jmx:Report/eb:Body/eb:Earthquake/eb:Hypocenter/eb:Area/eb:Name", nsManager)?.Value;
			var mag = document.XPathSelectElement("/jmx:Report/eb:Body/eb:Earthquake/jmx_eb:Magnitude", nsManager);
			if (mag?.Value == "NaN")
			{
				magnitude.Text = "";
				magnitudeSub.Text = mag.Attribute("description").Value;
			}
			else
			{
				magnitude.Text = mag?.Value;
				magnitudeSub.Text = "M";
			}

			objs.Add(new HypoCenterRenderObject(CoordinateConverter.GetLocation(coordinate), true));

			var intensity = JmaIntensityExtensions.ToJmaIntensity(document.XPathSelectElement("/jmx:Report/eb:Body/eb:Intensity/eb:Observation/eb:MaxInt", nsManager)?.Value);
			maxInt.Intensity = intensity;
			maxIntPanel.Background = (Brush)FindResource(intensity + "Background");
			maxIntensityDisplay.Foreground = (Brush)FindResource(intensity + "Foreground");

			var originTime = DateTimeOffset.Parse(document.XPathSelectElement("/jmx:Report/eb:Body/eb:Earthquake/eb:OriginTime", nsManager)?.Value);
			dateText.Text = originTime.ToString("yyyy年MM月dd日");
			timeText.Text = originTime.ToString("HH時mm分");

			foreach (var i in document.XPathSelectElements("/jmx:Report/eb:Body/eb:Intensity/eb:Observation/eb:Pref/eb:Area/eb:City/eb:IntensityStation", nsManager))
			{
				var code = i.XPathSelectElement("eb:Code", nsManager).Value;
				var station = Stations.Items.FirstOrDefault(s => s.Code == code);
				if (station == null)
					continue;
				var loc = station.GetLocation();
				objs.Add(new IntensityStationRenderObject()
				{
					Location = loc,
					Intensity = JmaIntensityExtensions.ToJmaIntensity(i.XPathSelectElement("eb:Int", nsManager).Value)
				});
				zoomPoints.Add(new Location(loc.Latitude - .1f, loc.Longitude - .1f));
				zoomPoints.Add(new Location(loc.Latitude + .1f, loc.Longitude + .1f));
			}
			objs.Sort((a, b) =>
			{
				if (a is not IntensityStationRenderObject ao || b is not IntensityStationRenderObject bo)
					return 0;
				return ao.Intensity - bo.Intensity;
			});

			objs.Add(hypoCenter);
			map.RenderObjects = objs.ToArray();

			var minLat = float.MaxValue;
			var maxLat = float.MinValue;
			var minLng = float.MaxValue;
			var maxLng = float.MinValue;
			foreach (var p in zoomPoints)
			{
				if (minLat > p.Latitude)
					minLat = p.Latitude;
				if (minLng > p.Longitude)
					minLng = p.Longitude;

				if (maxLat < p.Latitude)
					maxLat = p.Latitude;
				if (maxLng < p.Longitude)
					maxLng = p.Longitude;
			}
			var rect = new Rect(minLat, minLng, maxLat - minLat, maxLng - minLng);

			map.Navigate(rect, new Duration(TimeSpan.FromSeconds(.5)));
			stream.Dispose();
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
			var curPos = Mouse.GetPosition(map);
			if (Mouse.LeftButton == MouseButtonState.Pressed)
			{
				var diff = _prevPos - curPos;
				map.CenterLocation = (map.CenterLocation.ToPixel(map.Projection, map.Zoom) + diff).ToLocation(map.Projection, map.Zoom);
			}

			_prevPos = curPos;
			//var rect = map.PaddedRect;

			//var centerPos = map.CenterLocation.ToPixel(map.Projection, map.Zoom);
			//var mousePos = e.GetPosition(map);
			//var mouseLoc = new Point(centerPos.X + ((rect.Width / 2) - mousePos.X) + rect.Left, centerPos.Y + ((rect.Height / 2) - mousePos.Y) + rect.Top).ToLocation(map.Projection, map.Zoom);

			//mousePosition.Text = $"Mouse Lat: {mouseLoc.Latitude:0.000000} / Lng: {mouseLoc.Longitude:0.000000}";
		}
	}
}
