using Avalonia.Controls;
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
using System.Threading.Tasks;
using U8Xml;

namespace KyoshinEewViewer.Series.Earthquake;

public class EarthquakeSeries : SeriesBase
{
	public bool IsDebugBuiid { get; }
#if DEBUG
			= true;
#endif

	private OverlayLayer PointsLayer { get; } = new();
	private SoundPlayerService.SoundCategory SoundCategory { get; } = new("Earthquake", "地震情報");
	private SoundPlayerService.Sound UpdatedSound { get; }
	private SoundPlayerService.Sound UpdatedTrainingSound { get; }

	public EarthquakeSeries() : this(null, null) { }
	public EarthquakeSeries(NotificationService? notificationService, TelegramProvideService? telegramProvideService) : base("地震情報")
	{
		TelegramProvideService = telegramProvideService ?? Locator.Current.GetService<TelegramProvideService>() ?? throw new Exception("TelegramProvideService の解決に失敗しました");
		NotificationService = notificationService ?? Locator.Current.GetService<NotificationService>() ?? throw new Exception("notificationServiceの解決に失敗しました");
		Logger = LoggingService.CreateLogger(this);

		MapPadding = new Avalonia.Thickness(250, 0, 0, 0);
		Service = new EarthquakeWatchService(NotificationService, TelegramProvideService);

		UpdatedSound = SoundPlayerService.RegisterSound(SoundCategory, "Updated", "地震情報の更新");
		UpdatedTrainingSound = SoundPlayerService.RegisterSound(SoundCategory, "TrainingUpdated", "地震情報の更新(訓練)");

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
				Place = "です",
				IsTraining = true
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

		Service.SourceSwitching += () =>
		{
			IsLoading = true;
		};
		Service.SourceSwitched += s =>
		{
			SourceString = s;
			if (ConfigurationService.Current.Notification.SwitchEqSource)
				NotificationService.Notify("地震情報", s + "で地震情報を受信しています。");
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
			{
				ProcessEarthquake(eq);
				if (!eq.IsTraining || !UpdatedTrainingSound.Play())
					UpdatedSound.Play();
			}
		};
	}

	private Microsoft.Extensions.Logging.ILogger Logger { get; }
	private NotificationService NotificationService { get; }
	private TelegramProvideService TelegramProvideService { get; }

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
			if (App.MainWindow == null)
				return;
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
			foreach (var e in Service.Earthquakes.ToArray())
				e.IsSelecting = false;
			(PointsLayer.RenderObjects, CustomColorMap, ObservationIntensityGroups) = await ProcessXml(File.OpenRead(files[0]), eq);
			XmlParseError = null;
		}
		catch (Exception ex)
		{
			Logger.LogError("外部XMLの読み込みに失敗しました {ex}", ex);

			XmlParseError = ex;
			PointsLayer.RenderObjects = null;
			CustomColorMap = null;
			ObservationIntensityGroups = null;
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

		try
		{
			if (eq.UsedModels.Count > 0 && await InformationCacheService.GetTelegramAsync(eq.UsedModels[^1].Id) is Stream stream)
			{
				(PointsLayer.RenderObjects, CustomColorMap, ObservationIntensityGroups) = await ProcessXml(stream, eq);
				XmlParseError = null;
			}
			else
			{
				PointsLayer.RenderObjects = null;
				CustomColorMap = null;
			}
		}
		catch (Exception ex)
		{
			XmlParseError = ex;
			PointsLayer.RenderObjects = null;
			CustomColorMap = null;
			ObservationIntensityGroups = null;
		}
	}


	public async void ProcessHistoryXml(string id)
	{
		try
		{
			if (await InformationCacheService.GetTelegramAsync(id) is Stream stream)
			{
				(PointsLayer.RenderObjects, CustomColorMap, ObservationIntensityGroups) = await ProcessXml(stream, SelectedEarthquake);
				XmlParseError = null;
			}
		}
		catch (Exception ex)
		{
			XmlParseError = ex;
			PointsLayer.RenderObjects = null;
			CustomColorMap = null;
			ObservationIntensityGroups = null;
		}
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

			using var reader = XmlParser.Parse(body);

			// 震源に関する情報を解析する XMLからは処理しない
			HypoCenterRenderObject? ProcessHypocenter()
			{
				if (earthquake?.Location == null)
					return null;

				var hypoCenter = new HypoCenterRenderObject(earthquake.Location, false);
				objs.Add(new HypoCenterRenderObject(earthquake.Location, true));

				// 地震の規模に応じて表示範囲を変更する
				var size = .1f;
				if (earthquake?.Magnitude >= 4)
					size = .3f;
				if (!zoomPoints.Any())
					size = 30;

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

				if (!reader.Root.TryFindChild("Body", out var bodyNode))
					throw new Exception("Body がみつかりません");
				if (bodyNode.TryFindChild("Intensity", out var intensityNode))
				{
					if (!intensityNode.TryFindChild("Observation", out var observationNode))
						throw new Exception("Observation がみつかりません");
					// 都道府県
					foreach (var obs in observationNode.Children)
					{
						if (obs.Name.ToString() != "Pref")
							continue;

						var prefName = "取得失敗";
						string? prefCodeStr = null;
						var prefCode = 0;

						foreach (var pref in obs.Children)
						{
							switch (pref.Name.ToString())
							{
								case "Name":
									prefName = pref.InnerText.ToString();
									break;
								case "Code":
									prefCodeStr = pref.InnerText.ToString();
									if (!int.TryParse(prefCodeStr, out prefCode))
										throw new Exception("Pref.Code がパースできません");
									break;

								case "Area":
									// ここまできたらコードとかのパース終わってるはず
									if (prefCodeStr is null)
										throw new Exception("Pref.Code がみつかりません");

									var areaName = "取得失敗";
									string? areaCodeStr = null;
									var areaCode = 0;
									JmaIntensity areaIntensity = default;

									foreach (var area in pref.Children)
									{
										switch (area.Name.ToString())
										{
											case "Name":
												areaName = area.InnerText.ToString();
												break;
											case "Code":
												areaCodeStr = area.InnerText.ToString();
												if (!int.TryParse(areaCodeStr, out areaCode))
													throw new Exception("Area.Code がパースできません");
												break;
											case "MaxInt":
												areaIntensity = area.InnerText.ToString().Trim().ToJmaIntensity();
												break;

											case "City":
												// ここまできたらコードとかのパース終わってるはず
												if (areaCodeStr is null)
													throw new Exception("Area.Code がみつかりません");

												var cityName = "取得失敗";
												string? cityCodeStr = null;
												var cityCode = 0;
												JmaIntensity cityIntensity = default;

												foreach (var city in area.Children)
												{
													switch (city.Name.ToString())
													{
														case "Name":
															cityName = city.InnerText.ToString();
															break;
														case "Code":
															cityCodeStr = city.InnerText.ToString();
															if (!int.TryParse(cityCodeStr, out cityCode))
																throw new Exception("City.Code がパースできません");
															break;
														case "MaxInt":
															cityIntensity = city.InnerText.ToString().Trim().ToJmaIntensity();
															break;

														case "IntensityStation":
															// ここまできたらコードとかのパース終わってるはず
															if (cityCodeStr is null)
																throw new Exception("City.Code がみつかりません");

															var stationName = "取得失敗";
															string? stationCodeStr = null;
															var stationCode = 0;
															JmaIntensity stationIntensity = default;

															// 観測点
															foreach (var station in city.Children)
															{
																switch (station.Name.ToString())
																{
																	case "Name":
																		stationName = station.InnerText.ToString();
																		break;
																	case "Code":
																		stationCodeStr = station.InnerText.ToString();
																		if (!int.TryParse(stationCodeStr, out stationCode))
																			throw new Exception("IntensityStation.Code がパースできません");
																		break;
																	case "Int":
																		stationIntensity = station.InnerText.ToString().Trim().ToJmaIntensity();
																		break;
																}
															}

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
															break;
													}
												}

												// 色塗り用のデータをセット
												if (ConfigurationService.Current.Earthquake.FillDetail)
													mapMun[cityCode] = FixedObjectRenderer.IntensityPaintCache[cityIntensity].b.Color;

												// 観測点座標の定義が存在しない場合
												if (Service.Stations == null)
												{
													var cityLoc = RegionCenterLocations.Default.GetLocation(LandLayerType.MunicipalityEarthquakeTsunamiArea, cityCode);
													if (cityLoc == null)
														continue;
													objs.Add(new IntensityStationRenderObject(
														LandLayerType.MunicipalityEarthquakeTsunamiArea,
														cityName,
														cityLoc,
														cityIntensity,
														true));
													zoomPoints.Add(new KyoshinMonitorLib.Location(cityLoc.Latitude - .1f, cityLoc.Longitude - .1f));
													zoomPoints.Add(new KyoshinMonitorLib.Location(cityLoc.Latitude + .1f, cityLoc.Longitude + .1f));
												}
												break;
										}
									}

									var areaLoc = RegionCenterLocations.Default.GetLocation(LandLayerType.EarthquakeInformationSubdivisionArea, areaCode);
									if (areaLoc != null)
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

										if (areaLoc != null)
										{
											zoomPoints.Add(new KyoshinMonitorLib.Location(areaLoc.Latitude - .1f, areaLoc.Longitude - 1f));
											zoomPoints.Add(new KyoshinMonitorLib.Location(areaLoc.Latitude + .1f, areaLoc.Longitude + 1f));
										}
										if (ConfigurationService.Current.Earthquake.FillSokuhou)
											mapSub[areaCode] = FixedObjectRenderer.IntensityPaintCache[areaIntensity].b.Color;
									}
									break;
							}
						}
					}
				}

				colorMap[LandLayerType.EarthquakeInformationSubdivisionArea] = mapSub;
				colorMap[LandLayerType.MunicipalityEarthquakeTsunamiArea] = mapMun;
			}

			if (!reader.Root.TryFindChild("Control", out var controlNode))
				throw new Exception("Control がみつかりません");
			if (!controlNode.TryFindChild("Title", out var titleNode))
				throw new Exception("Title がみつかりません");

			switch (titleNode.InnerText.ToString())
			{
				case "震源・震度に関する情報":
					ProcessDetailPoints(false);
					break;
				case "震度速報":
					ProcessDetailPoints(true);
					break;
				default:
					throw new Exception($"この種類の電文を処理することはできません({titleNode.InnerText})");
			}

			var hypoCenter = ProcessHypocenter();

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
	[Reactive]
	public Exception? XmlParseError { get; set; }
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
