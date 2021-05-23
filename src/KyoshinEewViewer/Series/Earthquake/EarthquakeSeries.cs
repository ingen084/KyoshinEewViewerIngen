using Avalonia.Controls;
using Avalonia.Threading;
using DmdataSharp.ApiResponses.V2.Parameters;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Series.Earthquake.RenderObjects;
using KyoshinEewViewer.Series.Earthquake.Services;
using KyoshinEewViewer.Services;
using KyoshinMonitorLib;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace KyoshinEewViewer.Series.Earthquake
{
	public class EarthquakeSeries : SeriesBase
	{
		public EarthquakeSeries() : base("地震情報β")
		{
			MapPadding = new Avalonia.Thickness(250, 0, 0, 0);
			IsEnabled = false;
			Service = new EarthquakeWatchService();
			if (Design.IsDesignMode)
			{
				IsLoading = false;
				Service.Earthquakes.Add(new Models.Earthquake("a")
				{
					IsSokuhou = true,
					IsReportTime = true,
					IsHypocenterOnly = true,
					OccurrenceTime = DateTime.Now,
					Depth = 0,
					Intensity = JmaIntensity.Int0,
					Magnitude = 3.1f,
					Place = "これはサンプルデータです",
				});
				Service.Earthquakes.Add(SelectedEarthquake = new Models.Earthquake("b")
				{
					OccurrenceTime = DateTime.Now,
					Depth = -1,
					Intensity = JmaIntensity.Int4,
					Magnitude = 6.1f,
					Place = "デザイナ",
					IsSelecting = true
				});
				Service.Earthquakes.Add(new Models.Earthquake("c")
				{
					OccurrenceTime = DateTime.Now,
					Depth = 60,
					Intensity = JmaIntensity.Int5Lower,
					Magnitude = 3.0f,
					Place = "サンプル"
				});
				Service.Earthquakes.Add(new Models.Earthquake("d")
				{
					OccurrenceTime = DateTime.Now,
					Depth = 90,
					Intensity = JmaIntensity.Int6Upper,
					Magnitude = 6.1f,
					Place = "ViewModel"
				});
				Service.Earthquakes.Add(new Models.Earthquake("e")
				{
					OccurrenceTime = DateTime.Now,
					Depth = 450,
					Intensity = JmaIntensity.Int7,
					Magnitude = 6.1f,
					Place = "です"
				});
				return;
			}
		}

		private EarthquakeView? control;
		public override Control DisplayControl => control ?? throw new InvalidOperationException("初期化前にコントロールが呼ばれています");

		public bool IsActivate { get; set; }

		public override async void Activating()
		{
			IsActivate = true;
			if (control != null)
				return;
			control = new EarthquakeView
			{
				DataContext = this
			};
			Service.EarthquakeUpdated += eq => ProcessEarthquake(eq);
			await Service.StartAsync();
			IsLoading = false;
			if (Service.Earthquakes.Count > 0)
				ProcessEarthquake(Service.Earthquakes[0]);
		}

		public override void Deactivated()
		{
			IsActivate = false;
		}

		async Task OpenXML()
		{
			var ofd = new OpenFileDialog();
			ofd.Filters.Add(new FileDialogFilter
			{
				Name = "防災情報XML",
				Extensions = new List<string>
				{
					"xml"
				},
			});
			ofd.AllowMultiple = false;
			var files = await ofd.ShowAsync(App.MainWindow);
			if (files.Length <= 0 || string.IsNullOrWhiteSpace(files[0]))
				return;
			if (!File.Exists(files[0]))
				return;
			var eq = await Service.ProcessInformationAsync(new InformationHeader(InformationSource.Jma, "", "", DateTime.Now, null), File.OpenRead(files[0]), true);
			SelectedEarthquake = eq;
			RenderObjects = await ProcessXml(File.OpenRead(files[0]));
			foreach (var e in Service.Earthquakes)
				e.IsSelecting = false;
		}


		public void EarthquakeClicked(Models.Earthquake eq) => ProcessEarthquake(eq);

		public async void ProcessEarthquake(Models.Earthquake eq)
		{
			if (eq.UsedModels.Count <= 0 || control == null)
				return;
			foreach (var e in Service.Earthquakes)
				if (e != eq)
					e.IsSelecting = false;
			eq.IsSelecting = true;
			SelectedEarthquake = eq;
			RenderObjects = await ProcessXml(await InformationProviderService.Default.FetchContentAsync(eq.UsedModels[^1]));
		}

		EarthquakeStationParameterResponse? Stations { get; set; }

		//TODO 仮
		public async Task<IRenderObject[]> ProcessXml(Stream body)
		{
			using (body)
			{
				var started = DateTime.Now;

				var objs = new List<IRenderObject>();
				var zoomPoints = new List<KyoshinMonitorLib.Location>();

				XDocument document;
				XmlNamespaceManager nsManager;

				// 震源に関する情報を解析する
				HypoCenterRenderObject ProcessHypocenter()
				{
					var coordinate = document.XPathSelectElement("/jmx:Report/eb:Body/eb:Earthquake/eb:Hypocenter/eb:Area/jmx_eb:Coordinate", nsManager)?.Value;
					if (CoordinateConverter.GetLocation(coordinate) is not KyoshinMonitorLib.Location hc)
						throw new Exception("hypocenter取得失敗");

					var hypoCenter = new HypoCenterRenderObject(hc, false);
					objs.Add(new HypoCenterRenderObject(hc, true));

					zoomPoints.Add(new KyoshinMonitorLib.Location(hypoCenter.Location.Latitude - .3f, hypoCenter.Location.Longitude - .3f));
					zoomPoints.Add(new KyoshinMonitorLib.Location(hypoCenter.Location.Latitude + .3f, hypoCenter.Location.Longitude + .3f));

					return hypoCenter;
				}
				// 観測点に関する情報を解析する
				void ProcessDetailpoints(bool onlyAreas)
				{
					foreach (var i in document.XPathSelectElements("/jmx:Report/eb:Body/eb:Intensity/eb:Observation/eb:Pref/eb:Area", nsManager))
					{
						var codeStr = i.XPathSelectElement("eb:Code", nsManager)?.Value;
						if (!int.TryParse(codeStr, out var code))
							continue;
						var loc = RegionCenterLocations.Default.GetLocation(LandLayerType.EarthquakeInformationSubdivisionArea, code);
						if (loc == null)
							continue;

						var name = i.XPathSelectElement("eb:Name", nsManager)?.Value;
						//.Replace("都", "").Replace("道", "").Replace("府", "").Replace("県", "");

						objs.Add(new IntensityStationRenderObject(
							onlyAreas ? null : LandLayerType.EarthquakeInformationSubdivisionArea,
							name ?? "取得失敗",
							loc,
							JmaIntensityExtensions.ToJmaIntensity(i.XPathSelectElement("eb:MaxInt", nsManager)?.Value?.Trim() ?? "?"),
							true));
						zoomPoints.Add(new KyoshinMonitorLib.Location(loc.Latitude - .1f, loc.Longitude - .1f));
						zoomPoints.Add(new KyoshinMonitorLib.Location(loc.Latitude + .1f, loc.Longitude + .1f));
					}
					if (onlyAreas)
						return;

					if (Stations != null)
						foreach (var i in document.XPathSelectElements("/jmx:Report/eb:Body/eb:Intensity/eb:Observation/eb:Pref/eb:Area/eb:City/eb:IntensityStation", nsManager))
						{
							var code = i.XPathSelectElement("eb:Code", nsManager)?.Value;
							var station = Stations?.Items?.FirstOrDefault(s => s.Code == code);
							if (station == null)
								continue;
							if (station.GetLocation() is not KyoshinMonitorLib.Location loc)
								continue;
							objs.Add(new IntensityStationRenderObject(
								LandLayerType.MunicipalityEarthquakeTsunamiArea,
								i.XPathSelectElement("eb:Name", nsManager)?.Value ?? "取得失敗",
								loc,
								JmaIntensityExtensions.ToJmaIntensity(i.XPathSelectElement("eb:Int", nsManager)?.Value?.Trim() ?? "?"),
								false));
							zoomPoints.Add(new KyoshinMonitorLib.Location(loc.Latitude - .1f, loc.Longitude - .1f));
							zoomPoints.Add(new KyoshinMonitorLib.Location(loc.Latitude + .1f, loc.Longitude + .1f));
						}
					else
						foreach (var i in document.XPathSelectElements("/jmx:Report/eb:Body/eb:Intensity/eb:Observation/eb:Pref/eb:Area/eb:City", nsManager))
						{
							var codeStr = i.XPathSelectElement("eb:Code", nsManager)?.Value;
							if (!int.TryParse(codeStr, out var code))
								continue;
							var loc = RegionCenterLocations.Default.GetLocation(LandLayerType.MunicipalityEarthquakeTsunamiArea, code);
							if (loc == null)
								continue;
							objs.Add(new IntensityStationRenderObject(
								LandLayerType.MunicipalityEarthquakeTsunamiArea,
								i.XPathSelectElement("eb:Name", nsManager)?.Value ?? "取得失敗",
								loc,
								JmaIntensityExtensions.ToJmaIntensity(i.XPathSelectElement("eb:MaxInt", nsManager)?.Value?.Trim() ?? "?"),
								true));
							zoomPoints.Add(new KyoshinMonitorLib.Location(loc.Latitude - .1f, loc.Longitude - .1f));
							zoomPoints.Add(new KyoshinMonitorLib.Location(loc.Latitude + .1f, loc.Longitude + .1f));
						}
				}

				using (var reader = XmlReader.Create(body, new XmlReaderSettings { Async = true }))
				{
					document = await XDocument.LoadAsync(reader, LoadOptions.None, CancellationToken.None);
					nsManager = new XmlNamespaceManager(reader.NameTable);
				}
				nsManager.AddNamespace("jmx", "http://xml.kishou.go.jp/jmaxml1/");
				nsManager.AddNamespace("eb", "http://xml.kishou.go.jp/jmaxml1/body/seismology1/");
				nsManager.AddNamespace("jmx_eb", "http://xml.kishou.go.jp/jmaxml1/elementBasis1/");

				var title = document.XPathSelectElement("/jmx:Report/jmx:Control/jmx:Title", nsManager)?.Value;
				HypoCenterRenderObject? hypoCenter = null;

				switch (title)
				{
					case "震源・震度に関する情報":
						hypoCenter = ProcessHypocenter();
						ProcessDetailpoints(false);
						break;
					case "震度速報":
						ProcessDetailpoints(true);
						break;
					case "震源に関する情報":
						hypoCenter = ProcessHypocenter();
						break;
					default:
						return Array.Empty<IRenderObject>();
				}


				objs.Sort((a, b) =>
				{
					if (a is not IntensityStationRenderObject ao || b is not IntensityStationRenderObject bo)
						return 0;

					if (hypoCenter == null)
						return ao.Intensity - bo.Intensity;
					return (ao.Intensity - bo.Intensity) * 10000 +
						(int)(Math.Sqrt(Math.Pow(bo.Location.Latitude - hypoCenter.Location.Latitude, 2) + Math.Pow(bo.Location.Longitude - hypoCenter.Location.Longitude, 2)) * 1000) -
						(int)(Math.Sqrt(Math.Pow(ao.Location.Latitude - hypoCenter.Location.Latitude, 2) + Math.Pow(ao.Location.Longitude - hypoCenter.Location.Longitude, 2)) * 1000);
				});
				if (hypoCenter != null)
					objs.Add(hypoCenter);

				if (zoomPoints.Any())
				{
					// 自動ズーム範囲を計算
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
					var rect = new Avalonia.Rect(minLat, minLng, maxLat - minLat, maxLng - minLng);

					FocusBound = rect;
				}

				return objs.ToArray();
			}
		}


		[Reactive]
		public Models.Earthquake? SelectedEarthquake { get; set; }

		public EarthquakeWatchService Service { get; }

		[Reactive]
		public bool IsLoading { get; set; } = true;
	}
}
