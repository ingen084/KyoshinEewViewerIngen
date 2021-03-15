using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using DmdataSharp;
using DmdataSharp.ApiResponses.V1.Parameters;
using EarthquakeRenderTest.RenderObjects;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Map;
using KyoshinMonitorLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace EarthquakeRenderTest.Views
{
	public class MainWindow : Window
	{
		public MainWindow()
		{
			ApiClient = new DmdataV1ApiClient(Environment.GetEnvironmentVariable("DMDATA_APIKEY"), "Eqbot_test@ingen084");
			Socket = new DmdataV1Socket(ApiClient);

			InitializeComponent();
#if DEBUG
			this.AttachDevTools();
#endif
		}

		DmdataV1ApiClient ApiClient { get; }
		DmdataV1Socket Socket { get; }
		Point _prevPos;

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);

			//AllowDrop = true;
			//Drop += async (s, e) =>
			//{
			//	if (e.Data.GetData(DataFormats.FileDrop) is string[] files && File.Exists(files[0]) && files[0].EndsWith(".xml"))
			//	{
			//		using var fs = File.OpenRead(files[0]);
			//		await ProcessXml(fs);
			//	}
			//};
			//PreviewDragOver += (s, e) =>
			//{
			//	if (e.Data.GetDataPresent(DataFormats.FileDrop, true))
			//		e.Effects = DragDropEffects.Copy;
			//	else
			//		e.Effects = DragDropEffects.None;
			//	e.Handled = true;
			//};
#if DEBUG
			return;
#endif

			var map = this.FindControl<MapControl>("map");
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

			map.Zoom = 6;
			map.CenterLocation = new KyoshinMonitorLib.Location(36.474f, 135.264f);

			InitEq();

			var mainGrid = this.FindControl<Grid>("mainGrid");
			this.FindControl<Button>("saveButton").Click += (s, e) => SaveImage(mainGrid, $"{DateTime.Now:yyyyMMddHHmmss}.png");
			this.FindControl<Button>("sendButton").Click += async (s, e) => await SendWebhookAsync(mainGrid);
			this.FindControl<Button>("openButton").Click += async (s, e) =>
			{
				var ofd = new OpenFileDialog
				{
					Filters = new() { new() { Name = "XML", Extensions = new() { "xml" } } },
					AllowMultiple = false,
				};
				var files = await ofd.ShowAsync(this);
				if (!files.Any())
					return;
				using var stream = File.OpenRead(files[0]);
				await ProcessXml(stream);
			};

			//var stopwatch = Stopwatch.StartNew();
			//var previous = DateTime.Now;
			//CompositionTarget.Rendering += (s, e) =>
			//{
			//	var frameRate = 1 / (stopwatch.Elapsed - previous).TotalSeconds;
			//	if (frameRate > 0)
			//		fps.Text = frameRate.ToString(".0");
			//	previous = stopwatch.Elapsed;
			//};
		}

		EarthquakeStationParameterResponse Stations { get; set; }
		TimeSpan workTime;

		private async void InitEq()
		{
			Stations = await ApiClient.GetEarthquakeStationParameterAsync();

			var eqHistoryCombobox = this.FindControl<ComboBox>("eqHistoryCombobox");
			eqHistoryCombobox.SelectionChanged += async (s, e) =>
			{
				using var tStream = await ApiClient.GetTelegramStreamAsync(eqHistoryCombobox.SelectedItem as string);
				await ProcessXml(tStream);
			};

			var last = await ApiClient.GetTelegramListAsync(type: "VXSE53");
			var comboboxItems = new List<string>();
			foreach (var item in last.Items)
				comboboxItems.Add(item.Key);
			eqHistoryCombobox.Items = comboboxItems;
			eqHistoryCombobox.SelectedIndex = 0;

			Socket.DataReceived += async (s, e) =>
			{
				if (e.Data.Type != "VXSE53")
					return;
				using var stream = e.GetBodyStream();
				await ProcessXml(stream);
				var grid = await Dispatcher.UIThread.InvokeAsync(() => this.FindControl<Grid>("mainGrid"));
				await Dispatcher.UIThread.InvokeAsync(grid.InvalidateVisual);
				await SendWebhookAsync(grid);
			};
			await Socket.ConnectAsync(new[] { TelegramCategoryV1.Earthquake }, "Eqbot_test");
		}

		public async Task ProcessXml(Stream stream)
		{
			var started = DateTime.Now;

			var objs = new List<IRenderObject>();
			var zoomPoints = new List<KyoshinMonitorLib.Location>();

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
			if (title != "êkåπÅEêkìxÇ…ä÷Ç∑ÇÈèÓïÒ")
			{
				stream.Dispose();
				return;
			}

			await Dispatcher.UIThread.InvokeAsync(() => this.FindControl<TextBlock>("infoTitle").Text = title);

			var coordinate = document.Root.XPathSelectElement("/jmx:Report/eb:Body/eb:Earthquake/eb:Hypocenter/eb:Area/jmx_eb:Coordinate", nsManager)?.Value;
			var hypoCenter = new HypoCenterRenderObject(CoordinateConverter.GetLocation(coordinate), false);

			zoomPoints.Add(new KyoshinMonitorLib.Location(hypoCenter.Location.Latitude - .3f, hypoCenter.Location.Longitude - .3f));
			zoomPoints.Add(new KyoshinMonitorLib.Location(hypoCenter.Location.Latitude + .3f, hypoCenter.Location.Longitude + .3f));

			var depthNullable = CoordinateConverter.GetDepth(coordinate);

			await Dispatcher.UIThread.InvokeAsync(() =>
			{
				var depthBlock = this.FindControl<Grid>("depthBlock");
				var depthShallow = this.FindControl<TextBlock>("depthShallow");
				var depthDeep = this.FindControl<StackPanel>("depthDeep");
				var depthValue = this.FindControl<TextBlock>("depthValue");
				if (depthNullable is not int depth)
					depthBlock.IsVisible = false;
				else if (depth <= 0)
				{
					depthBlock.IsVisible = true;
					depthShallow.IsVisible = true;
					depthDeep.IsVisible = false;
				}
				else
				{
					depthBlock.IsVisible = true;
					depthShallow.IsVisible = false;
					depthDeep.IsVisible = true;
					depthValue.Text = depth.ToString();
				}

				var hypocenterName = this.FindControl<TextBlock>("hypocenterName");
				hypocenterName.Text = document.XPathSelectElement("/jmx:Report/eb:Body/eb:Earthquake/eb:Hypocenter/eb:Area/eb:Name", nsManager)?.Value;
				var mag = document.XPathSelectElement("/jmx:Report/eb:Body/eb:Earthquake/jmx_eb:Magnitude", nsManager);
				var magnitude = this.FindControl<TextBlock>("magnitude");
				var magnitudeSub = this.FindControl<TextBlock>("magnitudeSub");
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

				var intensity = KyoshinMonitorLib.JmaIntensityExtensions.ToJmaIntensity(document.XPathSelectElement("/jmx:Report/eb:Body/eb:Intensity/eb:Observation/eb:MaxInt", nsManager)?.Value);
				var maxInt = this.FindControl<IntensityIcon>("maxInt");
				var maxIntPanel = this.FindControl<StackPanel>("maxIntPanel");
				var maxIntensityDisplay = this.FindControl<TextBlock>("maxIntensityDisplay");
				maxInt.Intensity = intensity;
				maxIntPanel.Background = new SolidColorBrush((Color)this.FindResource(intensity + "Background"));
				maxIntensityDisplay.Foreground = new SolidColorBrush((Color)this.FindResource(intensity + "Foreground"));

				var originTime = DateTimeOffset.Parse(document.XPathSelectElement("/jmx:Report/eb:Body/eb:Earthquake/eb:OriginTime", nsManager)?.Value);
				var dateText = this.FindControl<TextBlock>("dateText");
				dateText.Text = originTime.ToString("yyyyîNMMåéddì˙");
				var timeText = this.FindControl<TextBlock>("timeText");
				timeText.Text = originTime.ToString("HHéûmmï™");
			});

			foreach (var i in document.XPathSelectElements("/jmx:Report/eb:Body/eb:Intensity/eb:Observation/eb:Pref/eb:Area/eb:City/eb:IntensityStation", nsManager))
			{
				var code = i.XPathSelectElement("eb:Code", nsManager).Value;
				var station = Stations.Items.FirstOrDefault(s => s.Code == code);
				if (station == null)
					continue;
				var loc = station.GetLocation();
				objs.Add(new IntensityStationRenderObject
				{
					Location = loc,
					Intensity = JmaIntensityExtensions.ToJmaIntensity(i.XPathSelectElement("eb:Int", nsManager).Value)
				});
				zoomPoints.Add(new KyoshinMonitorLib.Location(loc.Latitude - .1f, loc.Longitude - .1f));
				zoomPoints.Add(new KyoshinMonitorLib.Location(loc.Latitude + .1f, loc.Longitude + .1f));
			}
			objs.Sort((a, b) =>
			{
				if (a is not IntensityStationRenderObject ao || b is not IntensityStationRenderObject bo)
					return 0;
				return (int)(Math.Sqrt(Math.Pow(ao.Location.Latitude - bo.Location.Latitude, 2) + Math.Pow(ao.Location.Longitude - bo.Location.Longitude, 2)) * 10000);
			});

			await Dispatcher.UIThread.InvokeAsync(() => this.FindControl<TextBlock>("pointCount").Text = objs.Count.ToString());

			objs.Add(hypoCenter);
			await Dispatcher.UIThread.InvokeAsync(() => this.FindControl<MapControl>("map").RenderObjects = objs.ToArray());

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
			var rect = new RectD(minLat, minLng, maxLat - minLat, maxLng - minLng);

			//map.Navigate(rect, new Duration(TimeSpan.FromSeconds(.5)));
			await Dispatcher.UIThread.InvokeAsync(() => this.FindControl<MapControl>("map").Navigate(rect));


			var forecastComment = document.XPathSelectElement("/jmx:Report/eb:Body/eb:Comments/eb:ForecastComment", nsManager);
			await Dispatcher.UIThread.InvokeAsync(() =>
			{
				if (forecastComment != null)
				{
					this.FindControl<Grid>("tsunamiBlock").IsVisible = true;
					this.FindControl<TextBlock>("tsunamiInfo").Text = forecastComment.XPathSelectElement("eb:Text", nsManager).Value;
				}
				else
					this.FindControl<Grid>("tsunamiBlock").IsVisible = false;
			});

			workTime = DateTime.Now - started;
			Debug.WriteLine($"ProcessTime: {workTime.TotalMilliseconds:0.000}ms");
		}

		HttpClient HttpClient { get; } = new();
		string? Webhook { get; } = Environment.GetEnvironmentVariable("DISCORD_WEBHOOK");
		public async Task SendWebhookAsync(Control surface, double scale = 2)
		{
			if (string.IsNullOrWhiteSpace(Webhook))
				return;

			var sendstartTime = DateTime.Now;
			var measureTime = TimeSpan.Zero;
			var renderTime = TimeSpan.Zero;
			var saveTime = TimeSpan.Zero;

			var size = new Size(1280, 720);
			await Dispatcher.UIThread.InvokeAsync(() =>
			{
				surface.Measure(size);
				surface.Arrange(new Rect(size));
			});
			measureTime = DateTime.Now - sendstartTime;
			sendstartTime = DateTime.Now;

			using var memoryStream = new MemoryStream();
			await Dispatcher.UIThread.InvokeAsync(() =>
			{
				sendstartTime = DateTime.Now;

				using var renderTargetBitmap = new RenderTargetBitmap(new PixelSize((int)(size.Width * scale), (int)(size.Height * scale)), new Vector(96.0 * scale, 96.0 * scale));
				renderTargetBitmap.Render(surface);

				renderTime = DateTime.Now - sendstartTime;
				sendstartTime = DateTime.Now;

				renderTargetBitmap.Save(memoryStream);

				saveTime = DateTime.Now - sendstartTime;
			});
			memoryStream.Seek(0, SeekOrigin.Begin);
			var form = new MultipartFormDataContent
			{
				{ new StreamContent(memoryStream), "Document", "image.png" },
				{ new StringContent($"{{\"content\":\"XMLâêÕ+èââÒï`âÊ: {workTime.TotalMilliseconds:0.000}ms\\nçƒÉåÉCÉAÉEÉg: {measureTime.TotalMilliseconds:0.000}ms\\nï`âÊéûä‘: {renderTime.TotalMilliseconds:0.000}ms\\nâÊëúê∂ê¨éûä‘: {saveTime.TotalMilliseconds:0.000}ms\"}}"), "payload_json" },
			};
			await HttpClient.PostAsync(Webhook, form);
		}
		public void SaveImage(Control surface, string filename, double scale = 2)
		{
			if (filename == null)
				return;

			var size = new Size(1280, 720);
			surface.Measure(size);
			surface.Arrange(new Rect(size));

			using var renderTargetBitmap = new RenderTargetBitmap(new PixelSize((int)(size.Width * scale), (int)(size.Height * scale)), new Vector(96.0 * scale, 96.0 * scale));
			renderTargetBitmap.Render(surface);
			using var fileStream1 = File.Create(filename);
			renderTargetBitmap.Save(fileStream1);
		}
	}
}
