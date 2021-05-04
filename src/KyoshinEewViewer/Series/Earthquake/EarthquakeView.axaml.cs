using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using DmdataSharp.ApiResponses.V2.Parameters;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Series.Earthquake.RenderObjects;
using KyoshinMonitorLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace KyoshinEewViewer.Series.Earthquake
{
	public class EarthquakeView : UserControl
	{
		public EarthquakeView()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
		EarthquakeStationParameterResponse? Stations { get; set; }

		//TODO âº
		public async Task<IRenderObject[]> ProcessXml(Stream body)
		{
			var started = DateTime.Now;

			var objs = new List<IRenderObject>();
			var zoomPoints = new List<KyoshinMonitorLib.Location>();

			XDocument document;
			XmlNamespaceManager nsManager;

			using (var reader = XmlReader.Create(body, new XmlReaderSettings { Async = true }))
			{
				document = await XDocument.LoadAsync(reader, LoadOptions.None, CancellationToken.None);
				nsManager = new XmlNamespaceManager(reader.NameTable);
			}
			nsManager.AddNamespace("jmx", "http://xml.kishou.go.jp/jmaxml1/");
			nsManager.AddNamespace("eb", "http://xml.kishou.go.jp/jmaxml1/body/seismology1/");
			nsManager.AddNamespace("jmx_eb", "http://xml.kishou.go.jp/jmaxml1/elementBasis1/");

			var title = document.Root?.XPathSelectElement("/jmx:Report/jmx:Control/jmx:Title", nsManager)?.Value;
			if (title != "êkåπÅEêkìxÇ…ä÷Ç∑ÇÈèÓïÒ")
				return Array.Empty<IRenderObject>();

			await Dispatcher.UIThread.InvokeAsync(() => this.FindControl<TextBlock>("infoTitle").Text = title);

			var coordinate = document.Root?.XPathSelectElement("/jmx:Report/eb:Body/eb:Earthquake/eb:Hypocenter/eb:Area/jmx_eb:Coordinate", nsManager)?.Value;
			if (CoordinateConverter.GetLocation(coordinate) is not KyoshinMonitorLib.Location hc)
			{
				Console.WriteLine("hypocenteréÊìæé∏îs");
				return Array.Empty<IRenderObject>();
			}
			var hypoCenter = new HypoCenterRenderObject(hc, false);

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
					magnitudeSub.Text = mag?.Attribute("description")?.Value ?? "[è⁄ç◊ïsñæ]";
				}
				else
				{
					magnitude.Text = mag?.Value;
					magnitudeSub.Text = "M";
				}

				objs.Add(new HypoCenterRenderObject(hc, true));

				var intensity = JmaIntensityExtensions.ToJmaIntensity(document.XPathSelectElement("/jmx:Report/eb:Body/eb:Intensity/eb:Observation/eb:MaxInt", nsManager)?.Value ?? "?");
				var maxInt = this.FindControl<IntensityIcon>("maxInt");
				var maxIntPanel = this.FindControl<StackPanel>("maxIntPanel");
				var maxIntensityDisplay = this.FindControl<TextBlock>("maxIntensityDisplay");
				maxInt.Intensity = intensity;
				maxIntPanel.Background = new SolidColorBrush((Color?)this.FindResource(intensity + "Background") ?? Colors.White);
				maxIntensityDisplay.Foreground = new SolidColorBrush((Color?)this.FindResource(intensity + "Foreground") ?? Colors.Black);

				var dateText = this.FindControl<TextBlock>("dateText");
				var timeText = this.FindControl<TextBlock>("timeText");
				if (!DateTimeOffset.TryParse(document.XPathSelectElement("/jmx:Report/eb:Body/eb:Earthquake/eb:OriginTime", nsManager)?.Value, out var originTime))
				{
					dateText.Text = "";
					timeText.Text = originTime.ToString("éÊìæé∏îs");
					return;
				}
				dateText.Text = originTime.ToString("yyyyîNMMåéddì˙");
				timeText.Text = originTime.ToString("HHéûmmï™");
			});

			foreach (var i in document.XPathSelectElements("/jmx:Report/eb:Body/eb:Intensity/eb:Observation/eb:Pref/eb:Area/eb:City", nsManager))
			{
				var codeStr = i.XPathSelectElement("eb:Code", nsManager)?.Value;
				if (!int.TryParse(codeStr, out var code))
					continue;
				var loc = RegionCenterLocations.Default.GetLocation(LandLayerType.MunicipalityEarthquakeTsunamiArea, code);
				if (loc == null)
					continue;
				objs.Add(new IntensityStationRenderObject(loc, JmaIntensityExtensions.ToJmaIntensity(i.XPathSelectElement("eb:MaxInt", nsManager)?.Value ?? "?"), true));
				zoomPoints.Add(new KyoshinMonitorLib.Location(loc.Latitude - .1f, loc.Longitude - .1f));
				zoomPoints.Add(new KyoshinMonitorLib.Location(loc.Latitude + .1f, loc.Longitude + .1f));
			}
			//foreach (var i in document.XPathSelectElements("/jmx:Report/eb:Body/eb:Intensity/eb:Observation/eb:Pref/eb:Area/eb:City/eb:IntensityStation", nsManager))
			//{
			//	var code = i.XPathSelectElement("eb:Code", nsManager)?.Value;
			//	var station = Stations?.Items?.FirstOrDefault(s => s.Code == code);
			//	if (station == null)
			//		continue;
			//	if (station.GetLocation() is not KyoshinMonitorLib.Location loc)
			//		continue;
			//	objs.Add(new IntensityStationRenderObject(loc, JmaIntensityExtensions.ToJmaIntensity(i.XPathSelectElement("eb:Int", nsManager)?.Value ?? "?")));
			//	zoomPoints.Add(new KyoshinMonitorLib.Location(loc.Latitude - .1f, loc.Longitude - .1f));
			//	zoomPoints.Add(new KyoshinMonitorLib.Location(loc.Latitude + .1f, loc.Longitude + .1f));
			//}
			objs.Sort((a, b) =>
			{
				if (a is not IntensityStationRenderObject ao || b is not IntensityStationRenderObject bo)
					return 0;

				return (ao.Intensity - bo.Intensity) * 10000 +
					(int)(Math.Sqrt(Math.Pow(bo.Location.Latitude - hypoCenter.Location.Latitude, 2) + Math.Pow(bo.Location.Longitude - hypoCenter.Location.Longitude, 2)) * 1000) -
					(int)(Math.Sqrt(Math.Pow(ao.Location.Latitude - hypoCenter.Location.Latitude, 2) + Math.Pow(ao.Location.Longitude - hypoCenter.Location.Longitude, 2)) * 1000);
			});

			objs.Add(hypoCenter);

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
			//await Dispatcher.UIThread.InvokeAsync(() => this.FindControl<MapControl>("map").Navigate(rect));


			var forecastComment = document.XPathSelectElement("/jmx:Report/eb:Body/eb:Comments/eb:ForecastComment", nsManager);
			await Dispatcher.UIThread.InvokeAsync(() =>
			{
				if (forecastComment != null)
				{
					this.FindControl<Grid>("tsunamiBlock").IsVisible = true;
					this.FindControl<TextBlock>("tsunamiInfo").Text = forecastComment.XPathSelectElement("eb:Text", nsManager)?.Value ?? "(í√îgÇ…ä÷Ç∑ÇÈèÓïÒÇ™éÊìæÇ≈Ç´Ç‹ÇπÇÒ)";
				}
				else
					this.FindControl<Grid>("tsunamiBlock").IsVisible = false;
			});

			return objs.ToArray();
		}
	}
}
