﻿using Avalonia.Controls;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Layers;
using KyoshinEewViewer.Series.Earthquake.Models;
using KyoshinEewViewer.Series.Earthquake.RenderObjects;
using KyoshinEewViewer.Series.Earthquake.Services;
using KyoshinEewViewer.Services;
using KyoshinMonitorLib;
using Microsoft.Extensions.Logging;
using ReactiveUI.Fody.Helpers;
using SkiaSharp;
using Splat;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace KyoshinEewViewer.Series.Earthquake;

public class EarthquakeSeries : SeriesBase
{
	public bool IsDebugBuiid { get; }
#if DEBUG
			= true;
#endif

	private OverlayLayer PointsLayer { get; } = new();

	public EarthquakeSeries() : this(null) { }
	public EarthquakeSeries(NotificationService? notificationService) : base("地震情報")
	{
		NotificationService = notificationService ?? Locator.Current.GetService<NotificationService>() ?? throw new Exception("notificationServiceの解決に失敗しました");
		Logger = LoggingService.CreateLogger(this);

		MapPadding = new Avalonia.Thickness(250, 0, 0, 0);
		Service = new EarthquakeWatchService(NotificationService);

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

			var groups = new List<ObservationIntensityGroup>();

			groups.AddStation(JmaIntensity.Int2, "テスト1", 0, "テスト1-1-1", 0, "テスト1-1-1-1", 0);
			groups.AddStation(JmaIntensity.Int2, "テスト1", 0, "テスト1-1-1", 0, "テスト1-1-1-2", 1);
			groups.AddStation(JmaIntensity.Int2, "テスト1", 0, "テスト1-2-1", 1, "テスト1-2-1-1", 2);
			groups.AddStation(JmaIntensity.Int2, "テスト2", 1, "テスト2-1-1", 2, "テスト2-1-1-1", 3);

			groups.AddArea(JmaIntensity.Int1, "テスト3", 2, "テスト3-1", 3);

			ObservationIntensityGroups = groups.ToArray();
			return;
		}

		OverlayLayers = new[] { PointsLayer };

		Service.SourceSwitching += s =>
		{
			IsLoading = true;
			SourceString = s;
			if (ConfigurationService.Current.Notification.SwitchEqSource)
				NotificationService.Notify("地震情報", s + "で地震情報を受信しています。");
		};
		Service.SourceSwitched += () =>
		{
			IsLoading = false;
			if (Service.Earthquakes.Count <= 0)
			{
				SelectedEarthquake = null;
				return;
			}
			ProcessEarthquake(Service.Earthquakes[0]);
		};
		Service.EarthquakeUpdated += (eq, isBulkInserting) =>
		{
			if (!isBulkInserting)
				ProcessEarthquake(eq);
		};
		_ = Service.StartAsync();
	}

	private Microsoft.Extensions.Logging.ILogger Logger { get; }
	private NotificationService NotificationService { get; }

	private EarthquakeView? control;
	public override Control DisplayControl => control ?? throw new InvalidOperationException("初期化前にコントロールが呼ばれています");

	public bool IsActivate { get; set; }

	public override void Activating()
	{
		IsActivate = true;
		if (control != null)
			return;
		control = new EarthquakeView
		{
			DataContext = this
		};
		if (Service.Earthquakes.Count > 0 && !IsLoading)
			ProcessEarthquake(Service.Earthquakes[0]);
	}

	public override void Deactivated() => IsActivate = false;

	public async Task OpenXML()
	{
		try
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
			if (files == null || files.Length <= 0 || string.IsNullOrWhiteSpace(files[0]))
				return;
			if (!File.Exists(files[0]))
				return;
			var eq = await Service.ProcessInformationAsync("", File.OpenRead(files[0]), true);
			SelectedEarthquake = eq;
			(PointsLayer.RenderObjects, CustomColorMap, ObservationIntensityGroups) = await ProcessXml(File.OpenRead(files[0]), eq);
			foreach (var e in Service.Earthquakes.ToArray())
				e.IsSelecting = false;
		}
		catch (Exception ex)
		{
			Logger.LogError("外部XMLの読み込みに失敗しました {ex}", ex);
		}
	}

	public void EarthquakeClicked(Models.Earthquake eq)
	{
		if (!eq.IsSelecting)
			ProcessEarthquake(eq);
	}
	public async void ProcessEarthquake(Models.Earthquake eq)
	{
		if (control == null)
			return;
		foreach (var e in Service.Earthquakes.ToArray())
			if (e != null)
				e.IsSelecting = e == eq;
		SelectedEarthquake = eq;
		if (eq.UsedModels.Count > 0 && await InformationCacheService.GetTelegramAsync(eq.UsedModels[^1].Id) is Stream stream)
			(PointsLayer.RenderObjects, CustomColorMap, ObservationIntensityGroups) = await ProcessXml(stream, eq);
		else
		{
			PointsLayer.RenderObjects = null;
			CustomColorMap = null;
		}
	}


	public async void ProcessHistoryXml(string id)
	{
		if (await InformationCacheService.GetTelegramAsync(id) is Stream stream)
			(PointsLayer.RenderObjects, CustomColorMap, ObservationIntensityGroups) = await ProcessXml(stream, SelectedEarthquake);
	}
	//TODO 仮 内部でbodyはdisposeします
	private async Task<(IRenderObject[], Dictionary<LandLayerType, Dictionary<int, SKColor>>, ObservationIntensityGroup[])> ProcessXml(Stream body, Models.Earthquake? earthquake)
	{
		using (body)
		{
			var started = DateTime.Now;

			var colorMap = new Dictionary<LandLayerType, Dictionary<int, SKColor>>();
			var objs = new List<IRenderObject>();
			var zoomPoints = new List<KyoshinMonitorLib.Location>();
			var pointGroups = new List<ObservationIntensityGroup>();

			XDocument document;
			XmlNamespaceManager nsManager;

			// 震源に関する情報を解析する
			HypoCenterRenderObject? ProcessHypocenter()
			{
				// XMLから処理しない
				//var coordinate = document.XPathSelectElement("/jmx:Report/eb:Body/eb:Earthquake/eb:Hypocenter/eb:Area/jmx_eb:Coordinate", nsManager)?.Value;
				//if (CoordinateConverter.GetLocation(coordinate) is not KyoshinMonitorLib.Location hc)
				//	throw new Exception("hypocenter取得失敗");
				if (earthquake?.Location == null)
					return null;

				var hypoCenter = new HypoCenterRenderObject(earthquake.Location, false);
				objs.Add(new HypoCenterRenderObject(earthquake.Location, true));

				// 地震の規模に応じて表示範囲を変更する
				var size = .1f;
				if (earthquake?.Magnitude >= 4)
					size = .3f;
				if (earthquake?.Magnitude >= 6.5 && earthquake.Intensity == JmaIntensity.Unknown)
					size = 20;

				zoomPoints.Add(new KyoshinMonitorLib.Location(hypoCenter.Location.Latitude - size, hypoCenter.Location.Longitude - size));
				zoomPoints.Add(new KyoshinMonitorLib.Location(hypoCenter.Location.Latitude + size, hypoCenter.Location.Longitude + size));

				return hypoCenter;
			}
			// 観測点に関する情報を解析する
			void ProcessDetailPoints(bool onlyAreas)
			{
				// 細分区域
				var mapSub = new Dictionary<int, SKColor>();
				var mapMun = new Dictionary<int, SKColor>();

				// 都道府県
				foreach (var pref in document.XPathSelectElements("/jmx:Report/eb:Body/eb:Intensity/eb:Observation/eb:Pref", nsManager))
				{
					var prefName = pref.XPathSelectElement("eb:Name", nsManager)?.Value ?? "取得失敗";
					var prefCodeStr = pref.XPathSelectElement("eb:Code", nsManager)?.Value;
					if (!int.TryParse(prefCodeStr, out var prefCode))
						continue;

					foreach (var area in pref.XPathSelectElements("eb:Area", nsManager))
					{
						var areaCodeStr = area.XPathSelectElement("eb:Code", nsManager)?.Value;
						if (!int.TryParse(areaCodeStr, out var areaCode))
							continue;
						var areaLoc = RegionCenterLocations.Default.GetLocation(LandLayerType.EarthquakeInformationSubdivisionArea, areaCode);
						if (areaLoc == null)
							continue;

						var areaName = area.XPathSelectElement("eb:Name", nsManager)?.Value ?? "取得失敗";

						var areaIntensity = JmaIntensityExtensions.ToJmaIntensity(area.XPathSelectElement("eb:MaxInt", nsManager)?.Value?.Trim() ?? "?");
						objs.Add(new IntensityStationRenderObject(
							onlyAreas ? null : LandLayerType.EarthquakeInformationSubdivisionArea,
							areaName,
							areaLoc,
							areaIntensity,
							true));

						// 震度速報など、細分区域単位でパースする場合
						if (onlyAreas)
						{
							pointGroups.AddArea(areaIntensity, prefName, prefCode, areaName, areaCode);

							zoomPoints.Add(new KyoshinMonitorLib.Location(areaLoc.Latitude - .1f, areaLoc.Longitude - 1f));
							zoomPoints.Add(new KyoshinMonitorLib.Location(areaLoc.Latitude + .1f, areaLoc.Longitude + 1f));
							if (ConfigurationService.Current.Earthquake.FillSokuhou)
								mapSub[areaCode] = FixedObjectRenderer.IntensityPaintCache[areaIntensity].b.Color;
							continue;
						}

						// 市町村
						foreach (var city in area.XPathSelectElements("eb:City", nsManager))
						{
							var cityName = city.XPathSelectElement("eb:Name", nsManager)?.Value ?? "取得失敗";
							var cityCodeStr = city.XPathSelectElement("eb:Code", nsManager)?.Value;
							if (!int.TryParse(cityCodeStr, out var cityCode))
								continue;
							var cityLoc = RegionCenterLocations.Default.GetLocation(LandLayerType.MunicipalityEarthquakeTsunamiArea, cityCode);
							if (cityLoc == null)
								continue;

							var cityIntensity = JmaIntensityExtensions.ToJmaIntensity(city.XPathSelectElement("eb:MaxInt", nsManager)?.Value?.Trim() ?? "?");
							if (ConfigurationService.Current.Earthquake.FillDetail)
								mapMun[cityCode] = FixedObjectRenderer.IntensityPaintCache[cityIntensity].b.Color;

							// 観測点座標の定義が存在しない場合
							if (Service.Stations == null)
							{
								objs.Add(new IntensityStationRenderObject(
									LandLayerType.MunicipalityEarthquakeTsunamiArea,
									city.XPathSelectElement("eb:Name", nsManager)?.Value ?? "取得失敗",
									cityLoc,
									cityIntensity,
									true));
								zoomPoints.Add(new KyoshinMonitorLib.Location(cityLoc.Latitude - .1f, cityLoc.Longitude - .1f));
								zoomPoints.Add(new KyoshinMonitorLib.Location(cityLoc.Latitude + .1f, cityLoc.Longitude + .1f));
							}

							// 観測点
							foreach (var i in city.XPathSelectElements("eb:IntensityStation", nsManager))
							{
								var stationCodeStr = i.XPathSelectElement("eb:Code", nsManager)?.Value;
								if (!int.TryParse(stationCodeStr, out var stationCode))
									continue;

								var stationIntensity = JmaIntensityExtensions.ToJmaIntensity(i.XPathSelectElement("eb:Int", nsManager)?.Value?.Trim() ?? "?");
								var stationName = i.XPathSelectElement("eb:Name", nsManager)?.Value ?? "取得失敗";

								pointGroups.AddStation(stationIntensity, prefName, prefCode, cityName, cityCode, stationName, stationCode);

								// 観測点座標の定義が存在する場合
								if (Service.Stations != null)
								{
									var station = Service.Stations.Items?.FirstOrDefault(s => s.Code == stationCodeStr);
									if (station == null)
										continue;
									if (station.GetLocation() is not KyoshinMonitorLib.Location stationLoc)
										continue;
									objs.Add(new IntensityStationRenderObject(
										LandLayerType.MunicipalityEarthquakeTsunamiArea,
										station.Name,
										stationLoc,
										stationIntensity,
										false));
									zoomPoints.Add(new KyoshinMonitorLib.Location(stationLoc.Latitude - .1f, stationLoc.Longitude - .1f));
									zoomPoints.Add(new KyoshinMonitorLib.Location(stationLoc.Latitude + .1f, stationLoc.Longitude + .1f));
								}
							}

						}
					}
				}

				colorMap[LandLayerType.EarthquakeInformationSubdivisionArea] = mapSub;
				colorMap[LandLayerType.MunicipalityEarthquakeTsunamiArea] = mapMun;
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
			var hypoCenter = ProcessHypocenter();

			switch (title)
			{
				case "震源・震度に関する情報":
					ProcessDetailPoints(false);
					break;
				case "震度速報":
					ProcessDetailPoints(true);
					break;
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

			return (objs.ToArray(), colorMap, pointGroups.ToArray());
		}
	}

	[Reactive]
	public bool IsHistoryShown { get; set; }


	[Reactive]
	public Models.Earthquake? SelectedEarthquake { get; set; }
	public EarthquakeWatchService Service { get; }

	[Reactive]
	public ObservationIntensityGroup[]? ObservationIntensityGroups { get; set; }

	[Reactive]
	public bool IsLoading { get; set; } = true;
	[Reactive]
	public bool IsFault { get; set; } = false;
	[Reactive]
	public string SourceString { get; set; } = "不明";
}
