using Avalonia.Controls;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Layers;
using KyoshinEewViewer.Series.Earthquake.Models;
using KyoshinEewViewer.Series.Earthquake.RenderObjects;
using KyoshinEewViewer.Series.Earthquake.Services;
using KyoshinEewViewer.Services;
using KyoshinMonitorLib;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SkiaSharp;
using Splat;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using U8Xml;
using Location = KyoshinMonitorLib.Location;

namespace KyoshinEewViewer.Series.Earthquake;

public class EarthquakeSeries : SeriesBase
{
	public bool IsDebugBuiid { get; }
#if DEBUG
			= true;
#endif

	private OverlayLayer PointsLayer { get; } = new();

	public EarthquakeSeries() : this(null, null) { }
	public EarthquakeSeries(NotificationService? notificationService, TelegramProvideService? telegramProvideService) : base("地震情報")
	{
		TelegramProvideService = telegramProvideService ?? Locator.Current.GetService<TelegramProvideService>() ?? throw new Exception("TelegramProvideService の解決に失敗しました");
		NotificationService = notificationService ?? Locator.Current.GetService<NotificationService>() ?? throw new Exception("notificationServiceの解決に失敗しました");
		Logger = LoggingService.CreateLogger(this);

		MapPadding = new Avalonia.Thickness(250, 0, 0, 0);
		Service = new EarthquakeWatchService(NotificationService, TelegramProvideService);

		MessageBus.Current.Listen<ProcessJmaEqdbRequested>().Subscribe(async x => await ProcessJmaEqdbAsync(x.Id));

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
			SelectedEarthquake = new Models.Earthquake("b")
			{
				OccurrenceTime = DateTime.Now,
				Depth = -1,
				Intensity = JmaIntensity.Int4,
				Magnitude = 6.1f,
				Place = "デザイナ",
				IsSelecting = true
			};
			Service.Earthquakes.Add(SelectedEarthquake);
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
			IsFault = false;
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
			ProcessEarthquake(Service.Earthquakes[0]).ConfigureAwait(false);
		};
		Service.EarthquakeUpdated += (eq, isBulkInserting) =>
		{
			if (!isBulkInserting)
				ProcessEarthquake(eq).ConfigureAwait(false);
		};
		Service.Failed += () =>
		{
			IsFault = true;
			IsLoading = false;
		};
	}

	public async Task Restart()
	{
		IsFault = false;
		IsLoading = true;
		await TelegramProvideService.RestoreAsync();
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
			ProcessEarthquake(Service.Earthquakes[0]).ConfigureAwait(false);
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
			var eq = Service.ProcessInformation("", File.OpenRead(files[0]), true);
			SelectedEarthquake = eq;
			foreach (var e in Service.Earthquakes.ToArray())
				e.IsSelecting = false;
			(PointsLayer.RenderObjects, CustomColorMap, ObservationIntensityGroups) = ProcessXml(File.OpenRead(files[0]), eq);
			XmlParseError = null;
		}
		catch (Exception ex)
		{
			Logger.LogWarning("外部XMLの読み込みに失敗しました {ex}", ex);

			XmlParseError = ex.Message;
			PointsLayer.RenderObjects = null;
			CustomColorMap = null;
			ObservationIntensityGroups = null;
		}
	}

	public void EarthquakeClicked(Models.Earthquake eq)
	{
		if (!eq.IsSelecting)
			ProcessEarthquake(eq).ConfigureAwait(false);
	}
	public async Task ProcessEarthquake(Models.Earthquake eq)
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
				(PointsLayer.RenderObjects, CustomColorMap, ObservationIntensityGroups) = ProcessXml(stream, eq);
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
			XmlParseError = ex.Message;
			PointsLayer.RenderObjects = null;
			CustomColorMap = null;
			ObservationIntensityGroups = null;
		}
	}


	public async Task ProcessHistoryXml(string id)
	{
		try
		{
			if (await InformationCacheService.GetTelegramAsync(id) is Stream stream)
			{
				(PointsLayer.RenderObjects, CustomColorMap, ObservationIntensityGroups) = ProcessXml(stream, SelectedEarthquake);
				XmlParseError = null;
			}
		}
		catch (Exception ex)
		{
			XmlParseError = ex.Message;
			PointsLayer.RenderObjects = null;
			CustomColorMap = null;
			ObservationIntensityGroups = null;
		}
	}
	// 仮 内部でbodyはdisposeします
	private (IRenderObject[], Dictionary<LandLayerType, Dictionary<int, SKColor>>, ObservationIntensityGroup[]) ProcessXml(Stream body, Models.Earthquake? earthquake)
	{
		using (body)
		{
			var colorMap = new Dictionary<LandLayerType, Dictionary<int, SKColor>>();
			var objs = new List<IRenderObject>();
			var zoomPoints = new List<Location>();
			var pointGroups = new List<ObservationIntensityGroup>();

			using var reader = XmlParser.Parse(body);

			// 震源に関する情報を解析する XMLからは処理しない
			HypoCenterRenderObject? ProcessHypocenter()
			{
				if (earthquake?.Location == null)
					return null;

				var hypoCenter = new HypoCenterRenderObject(earthquake.Location, false);
				objs.Add(new HypoCenterRenderObject(earthquake.Location, true));
				return hypoCenter;
			}
			// 観測点に関する情報を解析する
			void ProcessDetailPoints(bool onlyAreas)
			{
				// 細分区域
				var mapSub = new Dictionary<int, SKColor>();
				var mapMun = new Dictionary<int, SKColor>();

				if (!reader.Root.TryFindChild("Body", out var bodyNode))
					throw new EarthquakeTelegramParseException("Body がみつかりません");
				if (bodyNode.TryFindChild("Intensity", out var intensityNode))
				{
					if (!intensityNode.TryFindChild("Observation", out var observationNode))
						throw new EarthquakeTelegramParseException("Observation がみつかりません");
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
										throw new EarthquakeTelegramParseException("Pref.Code がパースできません");
									break;

								case "Area":
									// ここまできたらコードとかのパース終わってるはず
									if (prefCodeStr is null)
										throw new EarthquakeTelegramParseException("Pref.Code がみつかりません");

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
													throw new EarthquakeTelegramParseException("Area.Code がパースできません");
												break;
											case "MaxInt":
												areaIntensity = area.InnerText.ToString().Trim().ToJmaIntensity();
												break;

											case "City":
												// ここまできたらコードとかのパース終わってるはず
												if (areaCodeStr is null)
													throw new EarthquakeTelegramParseException("Area.Code がみつかりません");

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
																throw new EarthquakeTelegramParseException("City.Code がパースできません");
															break;
														case "MaxInt":
															cityIntensity = city.InnerText.ToString().Trim().ToJmaIntensity();
															break;

														case "IntensityStation":
															// ここまできたらコードとかのパース終わってるはず
															if (cityCodeStr is null)
																throw new EarthquakeTelegramParseException("City.Code がみつかりません");

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
																			throw new EarthquakeTelegramParseException("IntensityStation.Code がパースできません");
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
																if (station.GetLocation() is not Location stationLoc)
																	continue;
																objs.Add(new IntensityStationRenderObject(
																	LandLayerType.MunicipalityEarthquakeTsunamiArea,
																	station.Name,
																	stationLoc,
																	stationIntensity,
																	false));
																zoomPoints.Add(new Location(stationLoc.Latitude - .1f, stationLoc.Longitude - .1f));
																zoomPoints.Add(new Location(stationLoc.Latitude + .1f, stationLoc.Longitude + .1f));
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
													zoomPoints.Add(new Location(cityLoc.Latitude - .1f, cityLoc.Longitude - .1f));
													zoomPoints.Add(new Location(cityLoc.Latitude + .1f, cityLoc.Longitude + .1f));
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
											zoomPoints.Add(new Location(areaLoc.Latitude - .1f, areaLoc.Longitude - 1f));
											zoomPoints.Add(new Location(areaLoc.Latitude + .1f, areaLoc.Longitude + 1f));
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
				throw new EarthquakeTelegramParseException("Control がみつかりません");
			if (!controlNode.TryFindChild("Title", out var titleNode))
				throw new EarthquakeTelegramParseException("Title がみつかりません");

			var hypoCenter = ProcessHypocenter();

			switch (titleNode.InnerText.ToString())
			{
				case "震源・震度に関する情報":
					ProcessDetailPoints(false);
					break;
				case "震度速報":
					ProcessDetailPoints(true);
					break;
				default:
					throw new EarthquakeTelegramParseException($"この種類の電文を処理することはできません({titleNode.InnerText})");
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
			// 最後に震度アイコンが隠れることを防止するためのアイコンを挿入
			if (hypoCenter != null)
			{
				// 地震の規模に応じて表示範囲を変更する
				var size = .1f;
				if (earthquake?.Magnitude >= 4)
					size = .3f;
				if (!zoomPoints.Any())
					size = 30;

				zoomPoints.Add(new Location(hypoCenter.Location.Latitude - size, hypoCenter.Location.Longitude - size));
				zoomPoints.Add(new Location(hypoCenter.Location.Latitude + size, hypoCenter.Location.Longitude + size));

				objs.Add(hypoCenter);
			}

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

	public async Task ProcessJmaEqdbAsync(string eventId)
	{
		try
		{
			using var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All });
			using var response = await client.PostAsync("https://www.data.jma.go.jp/svd/eqdb/data/shindo/api/api.php", new FormUrlEncodedContent(new Dictionary<string, string>
		{
			{"mode", "event"},
			{"id", eventId},
		}));
			if (!response.IsSuccessStatusCode)
				throw new EarthquakeTelegramParseException("震度データベースからの取得に失敗しました: " + response.StatusCode);

			using var stream = await response.Content.ReadAsStreamAsync();
			var data = await JsonSerializer.DeserializeAsync<JmaEqdbData>(stream);
			if (data == null || data.Res == null)
				throw new EarthquakeTelegramParseException("震度データベースのレスポンスのパースに失敗しました");

			var objs = new List<IRenderObject>();
			var zoomPoints = new List<Location>();
			var pointGroups = new List<ObservationIntensityGroup>();
			var hypoCenters = new List<HypoCenterRenderObject>();

			Models.Earthquake? eq = null;

			if (data.Res.HypoCenters == null)
				throw new EarthquakeTelegramParseException("震源情報が見つかりません");

			// 震源情報をセット
			foreach (var hypo in data.Res.HypoCenters.Reverse())
			{
				if (eq == null)
					eq = new Models.Earthquake(hypo.Id ?? "");

				if (hypo.Location == null)
					continue;

				eq.Place = hypo.Name;
				if (!DateTime.TryParse(hypo.OccurrenceTime, out var ot))
					throw new EarthquakeTelegramParseException("日付がパースできません");
				eq.OccurrenceTime = ot;
				eq.Location = hypo.Location;
				eq.Intensity = hypo.MaxIntensity;
				eq.Depth = hypo.DepthKm ?? throw new EarthquakeTelegramParseException("震源の深さが取得できません");
				eq.Comment = "出典: 気象庁 震度データベース";
				if (float.TryParse(hypo.Magnitude, out var magnitude))
					eq.Magnitude = magnitude;
				else
					eq.MagnitudeAlternativeText = hypo.Magnitude;

				var hypoCenter = new HypoCenterRenderObject(hypo.Location, false);
				objs.Add(new HypoCenterRenderObject(hypo.Location, true));
				hypoCenters.Add(hypoCenter);
			}
			if (eq == null)
				throw new EarthquakeTelegramParseException("地震情報を組み立てることができませんでした");

			// 観測点情報をセット
			if (data.Res.IntensityStations == null)
				throw new EarthquakeTelegramParseException("震源情報が見つかりません");

			foreach (var st in data.Res.IntensityStations)
			{
				if (st.Location == null)
					continue;
				objs.Add(new IntensityStationRenderObject(
					LandLayerType.MunicipalityEarthquakeTsunamiArea,
					st.Name ?? "不明",
					st.Location,
					st.Intensity,
					false,
					true));
				zoomPoints.Add(new Location(st.Location.Latitude - .1f, st.Location.Longitude - .1f));
				zoomPoints.Add(new Location(st.Location.Latitude + .1f, st.Location.Longitude + .1f));

				pointGroups.AddArea(st.Intensity, "-", 0, st.Name ?? "不明", int.Parse(st.Code));
			}

			var hcLoc = hypoCenters.LastOrDefault()?.Location;
			objs.Sort((a, b) =>
			{
				if (a is not IntensityStationRenderObject ao || b is not IntensityStationRenderObject bo)
					return 0;

				if (hcLoc == null)
					return ao.Intensity - bo.Intensity;
				return (ao.Intensity - bo.Intensity) * 10000 +
					(int)(Math.Sqrt(Math.Pow(bo.Location.Latitude - hcLoc.Latitude, 2) + Math.Pow(bo.Location.Longitude - hcLoc.Longitude, 2)) * 1000) -
					(int)(Math.Sqrt(Math.Pow(ao.Location.Latitude - hcLoc.Latitude, 2) + Math.Pow(ao.Location.Longitude - hcLoc.Longitude, 2)) * 1000);
			});
			// 最後に震度アイコンが隠れることを防止するためのアイコンを挿入
			if (hypoCenters.Any())
			{
				foreach (var hypoCenter in hypoCenters)
				{
					// 地震の規模に応じて表示範囲を変更する
					var size = .1f;
					if (eq?.Magnitude >= 4)
						size = .3f;
					if (!zoomPoints.Any())
						size = 30;

					zoomPoints.Add(new Location(hypoCenter.Location.Latitude - size, hypoCenter.Location.Longitude - size));
					zoomPoints.Add(new Location(hypoCenter.Location.Latitude + size, hypoCenter.Location.Longitude + size));

					objs.Add(hypoCenter);
				}
			}

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

			SelectedEarthquake = eq;
			foreach (var e in Service.Earthquakes.ToArray())
				e.IsSelecting = false;
			CustomColorMap = null;
			PointsLayer.RenderObjects = objs.ToArray();
			ObservationIntensityGroups = pointGroups.ToArray();
			XmlParseError = null;
		}
		catch (Exception ex)
		{
			XmlParseError = ex.Message;
			PointsLayer.RenderObjects = null;
			CustomColorMap = null;
			ObservationIntensityGroups = null;
		}
	}

	[Reactive]
	public bool IsHistoryShown { get; set; }


	[Reactive]
	public Models.Earthquake? SelectedEarthquake { get; set; }
	[Reactive]
	public string? XmlParseError { get; set; }
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
