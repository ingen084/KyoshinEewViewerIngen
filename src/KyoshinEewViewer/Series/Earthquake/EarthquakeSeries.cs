using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Events;
using KyoshinEewViewer.JmaXmlParser;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Data;
using KyoshinEewViewer.Map.Layers;
using KyoshinEewViewer.Series.Earthquake.Events;
using KyoshinEewViewer.Series.Earthquake.Models;
using KyoshinEewViewer.Series.Earthquake.Services;
using KyoshinEewViewer.Services;
using KyoshinMonitorLib;
using ReactiveUI;
using SkiaSharp;
using Splat;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reactive;
using System.Text.Json;
using System.Threading.Tasks;
using Location = KyoshinMonitorLib.Location;

namespace KyoshinEewViewer.Series.Earthquake;

public class EarthquakeSeries : SeriesBase
{
	public static SeriesMeta MetaData { get; } = new(typeof(EarthquakeSeries), "earthquake", "地震情報", new FontIconSource { Glyph = "\xf05a", FontFamily = new(Utils.IconFontName) }, true, "震源･震度情報を受信･表示します。");

	public bool IsDebugBuild { get; }
#if DEBUG
			= true;
#endif

	private ILogger Logger { get; }
	private KyoshinEewViewerConfiguration Config { get; }
	private InformationCacheService CacheService { get; }
	private NotificationService NotificationService { get; }
	private TelegramProvideService TelegramProvideService { get; }
	public EarthquakeWatchService Service { get; set; }

	private EarthquakeLayer EarthquakeLayer { get; } = new();
	private MapData? MapData { get; set; }

	public EarthquakeSeries(ILogManager logManager, KyoshinEewViewerConfiguration config, EarthquakeWatchService watchService, InformationCacheService cacheService, TelegramProvideService telegramProvider, NotificationService notifyService) : base(MetaData)
	{
		SplatRegistrations.RegisterLazySingleton<EarthquakeSeries>();

		Logger = logManager.GetLogger<EarthquakeSeries>();
		Config = config;
		CacheService = cacheService;
		TelegramProvideService = telegramProvider;
		NotificationService = notifyService;

		MapPadding = new Thickness(240, 0, 0, 0);
		IsHistoryShown = Config.Earthquake.ShowHistory;

		EarthquakeClicked = ReactiveCommand.Create<Models.Earthquake>(eq =>
		{
			if (!eq.IsSelecting)
				ProcessEarthquake(eq).ConfigureAwait(false);
		});
		ProcessHistoryXml = ReactiveCommand.CreateFromTask<string>(async id =>
		{
			try
			{
				if (await CacheService.GetTelegramAsync(id) is { } stream)
				{
					ProcessXml(stream, SelectedEarthquake);
					XmlParseError = null;
				}
			}
			catch (Exception ex)
			{
				XmlParseError = ex.Message;
				EarthquakeLayer.ClearPoints();
				CustomColorMap = null;
				ObservationIntensityGroups = null;
			}
		});

		Service = watchService;
		OverlayLayers = [EarthquakeLayer];

		Service.SourceSwitching += () =>
		{
			IsFault = false;
			IsLoading = true;
		};
		Service.SourceSwitched += s =>
		{
			SourceString = s;
			if (Config.Notification.SwitchEqSource)
				NotificationService?.Notify("地震情報", s + "で地震情報を受信しています。");
			IsLoading = false;
			if (Service.Earthquakes.Count <= 0)
			{
				SelectedEarthquake = null;
				return;
			}
			ProcessEarthquake(Service.Earthquakes[0]).ConfigureAwait(false);
		};
		Service.EarthquakeUpdated += async (eq, isBulkInserting) =>
		{
			if (isBulkInserting)
				return;
			await ProcessEarthquake(eq);
			MessageBus.Current.SendMessage(new EarthquakeInformationUpdated(eq));
			if (Config.Earthquake.SwitchAtUpdate)
				ActiveRequest.Send(this);
		};
		Service.Failed += () =>
		{
			IsFault = true;
			IsLoading = false;
		};

#if DEBUG
		if (!Design.IsDesignMode)
			return;

		IsLoading = false;
		Service.Earthquakes.Add(new Models.Earthquake("a")
		{
			IsSokuhou = true,
			IsTargetTime = true,
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

		groups.AddStation(JmaIntensity.Int2, "テスト1", 0, "テスト1-1-1", 0, "テスト1-1-1-1", "0");
		groups.AddStation(JmaIntensity.Int2, "テスト1", 0, "テスト1-1-1", 0, "テスト1-1-1-2", "1");
		groups.AddStation(JmaIntensity.Int2, "テスト1", 0, "テスト1-2-1", 1, "テスト1-2-1-1", "2");
		groups.AddStation(JmaIntensity.Int2, "テスト2", 1, "テスト2-1-1", 2, "テスト2-1-1-1", "3");

		groups.AddArea(JmaIntensity.Int1, "テスト3", 2, "テスト3-1", 3);

		ObservationIntensityGroups = groups.ToArray();
#endif
	}

	public async Task Restart()
	{
		IsFault = false;
		IsLoading = true;
		await TelegramProvideService.RestoreAsync();
	}

	private EarthquakeView? _control;
	public override Control DisplayControl => _control ?? throw new InvalidOperationException("初期化前にコントロールが呼ばれています");

	public override void Initialize()
	{
		MessageBus.Current.Listen<ProcessJmaEqdbRequested>().Subscribe(async x => await ProcessJmaEqdbAsync(x.Id));
		MessageBus.Current.Listen<MapLoaded>().Subscribe(x => MapData = x.Data);
	}

	public override void Activating()
	{
		if (_control != null || Service == null)
			return;
		_control = new EarthquakeView
		{
			DataContext = this
		};
		if (Service.Earthquakes.Count > 0 && !IsLoading)
			ProcessEarthquake(Service.Earthquakes[0]).ConfigureAwait(false);
	}

	public override void Deactivated() { }

	public async Task OpenXml()
	{
		try
		{
			if (App.MainWindow == null || _control == null || Service == null)
				return;
			var files = await _control.GetTopLevel().StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
			{
				Title = "任意のXML電文を開く",
				FileTypeFilter = new List<FilePickerFileType>()
				{
					FilePickerFileTypes.All,
				},
				AllowMultiple = false,
			});
			if (files is not { Count: > 0 } || !files[0].Name.EndsWith(".xml"))
				return;
			var eq = Service.ProcessInformation("", await files[0].OpenReadAsync(), true);
			SelectedEarthquake = eq;
			foreach (var e in Service.Earthquakes.ToArray())
				e.IsSelecting = false;
			ProcessXml(await files[0].OpenReadAsync(), eq);
			XmlParseError = null;
		}
		catch (Exception ex)
		{
			Logger?.LogWarning(ex, "外部XMLの読み込みに失敗しました");

			XmlParseError = ex.Message;
			EarthquakeLayer.ClearPoints();
			CustomColorMap = null;
			ObservationIntensityGroups = null;
		}
	}

	public ReactiveCommand<Models.Earthquake, Unit> EarthquakeClicked { get; }

	private async Task ProcessEarthquake(Models.Earthquake eq)
	{
		if (_control == null || Service == null)
			return;
		foreach (var e in Service.Earthquakes.ToArray())
			if (e != null)
				e.IsSelecting = e == eq;
		SelectedEarthquake = eq;

		try
		{
			if (eq.UsedModels.Count > 0 && await CacheService.GetTelegramAsync(eq.UsedModels[^1].Id) is { } stream)
			{
				ProcessXml(stream, eq);
				XmlParseError = null;
			}
			else
			{
				EarthquakeLayer.ClearPoints();
				CustomColorMap = null;
			}
		}
		catch (Exception ex)
		{
			XmlParseError = ex.Message;
			EarthquakeLayer.ClearPoints();
			CustomColorMap = null;
			ObservationIntensityGroups = null;
		}
	}

	public ReactiveCommand<string, Unit> ProcessHistoryXml { get; }

	// 仮 内部でbodyはdisposeします
	private void ProcessXml(Stream body, Models.Earthquake? earthquake)
	{
		using (body)
		{
			var colorMap = new Dictionary<LandLayerType, Dictionary<int, SKColor>>();
			var zoomPoints = new List<Location>();
			var hypocenters = new List<Location>();
			var areaItems = new Dictionary<JmaIntensity, List<(Location Location, string Name)>>();
			var cityItems = new Dictionary<JmaIntensity, List<(Location Location, string Name)>>();
			var stationItems = new Dictionary<JmaIntensity, List<(Location Location, string Name)>>();
			var pointGroups = new List<ObservationIntensityGroup>();

			using var report = new JmaXmlDocument(body);

			// 震源に関する情報を解析する XMLからは処理しない
			Location? ProcessHypocenter()
			{
				if (earthquake?.Location == null)
					return null;

				hypocenters.Add(earthquake.Location);
				return earthquake.Location;
			}
			// 観測点に関する情報を解析する
			void ProcessDetailPoints(bool onlyAreas)
			{
				if (report.EarthquakeBody.Intensity?.Observation is not { } observation)
					return;

				// 細分区域
				var mapSub = new Dictionary<int, SKColor>();
				var mapMun = new Dictionary<int, SKColor>();

				FeatureLayer? cityLayer = null;
				MapData?.TryGetLayer(LandLayerType.MunicipalityEarthquakeTsunamiArea, out cityLayer);
				FeatureLayer? areaLayer = null;
				MapData?.TryGetLayer(LandLayerType.EarthquakeInformationSubdivisionArea, out areaLayer);

				// 都道府県
				foreach (var pref in observation.Prefs)
				{
					foreach (var area in pref.Areas)
					{
						var areaIntensity = area.MaxInt?.ToJmaIntensity() ?? JmaIntensity.Unknown;

						foreach (var city in area.Cities)
						{
							var cityIntensity = city.MaxInt?.ToJmaIntensity() ?? JmaIntensity.Unknown;

							foreach (var station in city.IntensityStations)
							{
								var stationIntensity = station.Int?.ToJmaIntensity() ?? JmaIntensity.Unknown;

								pointGroups.AddStation(stationIntensity, pref.Name, pref.Code, city.Name, city.Code, station.Name, station.Code);

								// 観測点座標の定義が存在する場合
								if (Service?.Stations != null)
								{
									var stInfo = Service.Stations.Items?.FirstOrDefault(s => s.Code == station.Code);
									if (stInfo?.GetLocation() is not { } stationLoc)
										continue;
									if (!stationItems.TryGetValue(stationIntensity, out var stations))
										stationItems[stationIntensity] = stations = [];
									stations.Add((stationLoc, station.Name));

									zoomPoints.Add(new Location(stationLoc.Latitude - .1f, stationLoc.Longitude - .1f));
									zoomPoints.Add(new Location(stationLoc.Latitude + .1f, stationLoc.Longitude + .1f));
								}
							}

							// 色塗り用のデータをセット
							if (Config.Earthquake.FillDetail)
								mapMun[city.Code] = FixedObjectRenderer.IntensityPaintCache[cityIntensity].Background.Color;

							// 観測点座標の定義が存在しない場合
							var cityLoc = RegionCenterLocations.Default.GetLocation(LandLayerType.MunicipalityEarthquakeTsunamiArea, city.Code);
							if (cityLoc == null)
								continue;
							if (!cityItems.TryGetValue(cityIntensity, out var cities))
								cityItems[cityIntensity] = cities = [];
							cities.Add((cityLoc, city.Name));

							if (cityLayer == null)
							{
								zoomPoints.Add(new Location(cityLoc.Latitude - .1f, cityLoc.Longitude - .1f));
								zoomPoints.Add(new Location(cityLoc.Latitude + .1f, cityLoc.Longitude + .1f));
							}
							else
							{
								foreach(var cityPoly in cityLayer.FindPolygon(city.Code))
								{
									zoomPoints.Add(cityPoly.BoundingBox.TopLeft.CastLocation());
									zoomPoints.Add(cityPoly.BoundingBox.BottomRight.CastLocation());
								}
							}
						}

						var areaLoc = RegionCenterLocations.Default.GetLocation(LandLayerType.EarthquakeInformationSubdivisionArea, area.Code);
						if (areaLoc != null)
						{
							if (!areaItems.TryGetValue(areaIntensity, out var areas))
								areaItems[areaIntensity] = areas = [];
							areas.Add((areaLoc, area.Name));
						}

						// 震度速報など、細分区域単位でパースする場合
						if (onlyAreas)
						{
							pointGroups.AddArea(areaIntensity, pref.Name, pref.Code, area.Name, area.Code);

							if (areaLayer == null && areaLoc != null)
							{
								zoomPoints.Add(new Location(areaLoc.Latitude - .1f, areaLoc.Longitude - 1f));
								zoomPoints.Add(new Location(areaLoc.Latitude + .1f, areaLoc.Longitude + 1f));
							}
							if (areaLayer != null)
							{
								foreach(var p in areaLayer.FindPolygon(area.Code))
								{
									zoomPoints.Add(p.BoundingBox.TopLeft.CastLocation());
									zoomPoints.Add(p.BoundingBox.BottomRight.CastLocation());
								}
							}
							if (Config.Earthquake.FillSokuhou)
								mapSub[area.Code] = FixedObjectRenderer.IntensityPaintCache[areaIntensity].Background.Color;
						}
					}
				}

				colorMap[LandLayerType.EarthquakeInformationSubdivisionArea] = mapSub;
				colorMap[LandLayerType.MunicipalityEarthquakeTsunamiArea] = mapMun;
			}

			var hypocenter = ProcessHypocenter();

			switch (report.Control.Title)
			{
				case "震源・震度に関する情報":
					ProcessDetailPoints(false);
					break;
				case "震度速報":
					ProcessDetailPoints(true);
					break;
				default:
					throw new EarthquakeTelegramParseException($"この種類の電文を処理することはできません({report.Control.Title})");
			}

			if (hypocenter != null)
			{
				SortItems(hypocenter, areaItems);
				SortItems(hypocenter, cityItems);
				SortItems(hypocenter, stationItems);

				// 地震の規模に応じて表示範囲を変更する
				var size = .1f;
				if (earthquake?.Magnitude >= 4)
					size = .3f;
				if (earthquake?.Magnitude >= 6 && earthquake.IsForeign)
					size = 30;

				zoomPoints.Add(new Location(hypocenter.Latitude - size, hypocenter.Longitude - size));
				zoomPoints.Add(new Location(hypocenter.Latitude + size, hypocenter.Longitude + size));
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

			CustomColorMap = colorMap;
			ObservationIntensityGroups = pointGroups.OrderByDescending(g => g.Intensity switch { JmaIntensity.Unknown => (((int)JmaIntensity.Int5Lower) * 10) - 1, _ => ((int)g.Intensity) * 10 }).ToArray();
			EarthquakeLayer.UpdatePoints(hypocenters, areaItems, cityItems.Any() ? cityItems : null, stationItems.Any() ? stationItems : null);
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

			await using var stream = await response.Content.ReadAsStreamAsync();
			var data = await JsonSerializer.DeserializeAsync<JmaEqdbData>(stream);
			if (data?.Res == null)
				throw new EarthquakeTelegramParseException("震度データベースのレスポンスのパースに失敗しました");

			var stationItems = new Dictionary<JmaIntensity, List<(Location Location, string Name)>>();
			var zoomPoints = new List<Location>();
			var pointGroups = new List<ObservationIntensityGroup>();
			var hypocenters = new List<Location>();

			Models.Earthquake? eq = null;

			if (data.Res.HypoCenters == null)
				throw new EarthquakeTelegramParseException("震源情報が見つかりません");

			// 震源情報をセット
			foreach (var hypo in data.Res.HypoCenters.Reverse())
			{
				eq ??= new Models.Earthquake(hypo.Id ?? "");

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
				if (float.TryParse(hypo.Magnitude, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var magnitude))
					eq.Magnitude = magnitude;
				else
					eq.MagnitudeAlternativeText = hypo.Magnitude;

				hypocenters.Add(hypo.Location);
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

				if (!stationItems.TryGetValue(st.Intensity, out var stations))
					stationItems[st.Intensity] = stations = [];
				stations.Add((st.Location, st.Name ?? "不明"));

				zoomPoints.Add(new Location(st.Location.Latitude - .1f, st.Location.Longitude - .1f));
				zoomPoints.Add(new Location(st.Location.Latitude + .1f, st.Location.Longitude + .1f));

				pointGroups.AddArea(st.Intensity, "-", 0, st.Name ?? "不明", int.Parse(st.Code ?? "0"));
			}

			var hcLoc = hypocenters.LastOrDefault();
			if (hcLoc != null)
				SortItems(hcLoc, stationItems);


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
			EarthquakeLayer.UpdatePoints(hypocenters, null, null, stationItems);
			ObservationIntensityGroups = pointGroups.OrderByDescending(g => g.Intensity switch { JmaIntensity.Unknown => (((int)JmaIntensity.Int5Lower) * 10) - 1, _ => ((int)g.Intensity) * 10 }).ToArray();
			XmlParseError = null;
		}
		catch (Exception ex)
		{
			XmlParseError = ex.Message;
			EarthquakeLayer.ClearPoints();
			CustomColorMap = null;
			ObservationIntensityGroups = null;
		}
	}
	private static void SortItems(Location hypocenter, Dictionary<JmaIntensity, List<(Location Location, string Name)>> items)
	{
		foreach (var item in items)
			item.Value.Sort((a, b)
				=> (int)(Math.Sqrt(Math.Pow(b.Location.Latitude - hypocenter.Latitude, 2) + Math.Pow(b.Location.Longitude - hypocenter.Longitude, 2)) * 1000) -
				   (int)(Math.Sqrt(Math.Pow(a.Location.Latitude - hypocenter.Latitude, 2) + Math.Pow(a.Location.Longitude - hypocenter.Longitude, 2)) * 1000));
	}

	private bool _isHistoryShown;
	public bool IsHistoryShown
	{
		get => _isHistoryShown;
		set {
			if (this.RaiseAndSetIfChanged(ref _isHistoryShown, value))
				MapPadding = new Thickness(MapPadding.Left, MapPadding.Top, 240, MapPadding.Bottom);
			else
				MapPadding = new Thickness(MapPadding.Left, MapPadding.Top, 0, MapPadding.Bottom);
			Config.Earthquake.ShowHistory = value;
		}
	}

	private Models.Earthquake? _selectedEarthquake;
	public Models.Earthquake? SelectedEarthquake
	{
		get => _selectedEarthquake;
		set {
			_selectedEarthquake = value;
			// プロパティの変更がうまく反映されない時があるので強制的に更新させる
			this.RaisePropertyChanged();
		}
	}
	private string? _xmlParseError;
	public string? XmlParseError
	{
		get => _xmlParseError;
		set => this.RaiseAndSetIfChanged(ref _xmlParseError, value);
	}


	private ObservationIntensityGroup[]? _observationIntensityGroups;
	public ObservationIntensityGroup[]? ObservationIntensityGroups
	{
		get => _observationIntensityGroups;
		set => this.RaiseAndSetIfChanged(ref _observationIntensityGroups, value);
	}

	private bool _isLoading = true;
	public bool IsLoading
	{
		get => _isLoading;
		set => this.RaiseAndSetIfChanged(ref _isLoading, value);
	}
	private bool _isFault = false;
	public bool IsFault
	{
		get => _isFault;
		set => this.RaiseAndSetIfChanged(ref _isFault, value);
	}
	private string _sourceString = "不明";
	public string SourceString
	{
		get => _sourceString;
		set => this.RaiseAndSetIfChanged(ref _sourceString, value);
	}
}
