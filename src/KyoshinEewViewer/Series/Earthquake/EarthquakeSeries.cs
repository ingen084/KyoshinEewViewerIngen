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
using KyoshinEewViewer.Series.Earthquake.Events;
using KyoshinEewViewer.Series.Earthquake.Models;
using KyoshinEewViewer.Series.Earthquake.Services;
using KyoshinEewViewer.Series.Earthquake.SettingPages;
using KyoshinEewViewer.Series.Earthquake.Templates;
using KyoshinEewViewer.Series.Earthquake.Workflow;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.Services.TelegramPublishers;
using KyoshinEewViewer.Services.Workflows.BuiltinActions;
using WorkflowsNamespace = KyoshinEewViewer.Services.Workflows;
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
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Location = KyoshinMonitorLib.Location;

namespace KyoshinEewViewer.Series.Earthquake;

public class EarthquakeSeries : SeriesBase
{
	public static SeriesMeta MetaData { get; } = new(typeof(EarthquakeSeries), "earthquake", "地震情報", new FAFontIconSource { Glyph = "\xf05a", FontFamily = new(Utils.IconFontName) }, true, "震源･震度情報を受信･表示します。");

	public bool IsDebugBuild { get; }
#if DEBUG
			= true;
#endif

	private SoundCategory SoundCategory { get; } = new("Earthquake", "地震情報");
	private Sound UpdatedSound { get; }
	private Sound IntensityUpdatedSound { get; }
	private Sound UpdatedTrainingSound { get; }

	private ILogger Logger { get; }
	private KyoshinEewViewerConfiguration Config { get; }
	private NotificationService NotificationService { get; }
	private TelegramProvideService TelegramProvideService { get; }
	private WorkflowService WorkflowService { get; }
	public EarthquakeWatchService Service { get; set; }

	private EarthquakeLayer EarthquakeLayer { get; } = new();
	private MapData? MapData { get; set; }

	public EarthquakeSeries(
		ILogManager logManager,
		KyoshinEewViewerConfiguration config,
		EarthquakeWatchService watchService,
		WorkflowService workflowService,
		SoundPlayerService soundPlayer,
		TelegramProvideService telegramProvider,
		NotificationService notifyService) : base(MetaData)
	{
		SplatRegistrations.RegisterLazySingleton<EarthquakeSeries>();

		Logger = logManager.GetLogger<EarthquakeSeries>();
		Config = config;
		TelegramProvideService = telegramProvider;
		NotificationService = notifyService;
		WorkflowService = workflowService;

		UpdatedSound = soundPlayer.RegisterSound(SoundCategory, "Updated", "地震情報の更新", "{int}: 最大震度 [？,0,1,...,6-,6+,7]", new() { { "int", "4" }, });
		IntensityUpdatedSound = soundPlayer.RegisterSound(SoundCategory, "IntensityUpdated", "震度の更新", "{int}: 最大震度 [？,0,1,...,6-,6+,7]", new() { { "int", "4" }, });
		UpdatedTrainingSound = soundPlayer.RegisterSound(SoundCategory, "TrainingUpdated", "地震情報の更新(訓練)", "{int}: 最大震度 [？,0,1,...,6-,6+,7]", new() { { "int", "6+" }, });

		//ProcessHistoryXml = ReactiveCommand.CreateFromTask<string>(async id =>
		//{
		//	try
		//	{
		//		if (await CacheService.GetTelegramAsync(id) is { } stream)
		//		{
		//			ProcessXml(stream, SelectedEarthquake);
		//			TelegramProcessError = null;
		//		}
		//	}
		//	catch (Exception ex)
		//	{
		//		TelegramProcessError = ex.Message;
		//		EarthquakeLayer.ClearPoints();
		//		CustomColorMap = null;
		//		ObservationIntensityGroups = null;
		//	}
		//});

		Service = watchService;

		MapDisplayParameter = new() {
			Padding = new(240, 0, 0, 0),
			OverlayLayers = [EarthquakeLayer],
		};
		IsHistoryShown = Config.Earthquake.ShowHistory;

		Service.SourceSwitching
			.ObserveOn(RxSchedulers.MainThreadScheduler)
			.Subscribe(_ =>
			{
				IsFault = false;
				IsLoading = true;
			});

		Service.SourceSwitched
			.ObserveOn(RxSchedulers.MainThreadScheduler)
			.Select(s => Observable.FromAsync(async () =>
			{
				SourceString = s;
				if (Config.Notification.SwitchEqSource)
					NotificationService?.Notify("地震情報", s + "で地震情報を受信しています。");
				IsLoading = false;
				if (Service.Earthquakes.Count <= 0)
				{
					CurrentEvent = null;
					return;
				}
				await ProcessEarthquakeEvent(Service.Earthquakes[0]);
			}))
			.Concat()
			.Subscribe(_ => { }, ex => Logger.LogError(ex, "受信元切替後の処理中に例外が発生しました"));

		Service.EarthquakeUpdated
			.Where(u => !u.IsBulkInserting)
			.ObserveOn(RxSchedulers.MainThreadScheduler)
			.Select(u => Observable.FromAsync(async () =>
			{
				var eq = u.Earthquake;
				var fragment = u.Fragment;
				var prevInt = u.PreviousMaxIntensity;

				await ProcessEarthquakeEvent(eq);
				MessageBus.Current.SendMessage(new EarthquakeInformationUpdated(eq));

				if (fragment == null)
					return;
				workflowService.PublishEvent(new EarthquakeInformationEvent(this)
				{
					UpdatedAt = eq.UpdatedTime,
					LatestInformationName = fragment.Title,

					EarthquakeId = eq.EventId,
					IsTrainingOrTest = eq.IsTraining || eq.IsTest,
					IsVolcano = eq.IsVolcano,
					VolcanoName = eq.VolcanoName,
					DetectedAt = eq.IsDetectionTime ? eq.Time : null,

					MaxIntensity = eq.Intensity,
					PreviousMaxIntensity = prevInt,
					MaxLpgmIntensity = eq.LpgmIntensity,
					Hypocenter = eq.IsHypocenterAvailable ? new(
						eq.Time,
						eq.Place,
						eq.Location,
						eq.Magnitude,
						eq.MagnitudeAlternativeText,
						eq.Depth,
						eq.IsNoDepthData,
						eq.IsVeryShallow,
						eq.IsForeign
					) : null,

					Comment = eq.Comment,
					FreeFormComment = eq.FreeFormComment,

					IsCancelled = eq.IsCancelled,
					IsHypocenterOnly = eq.IsHypocenterOnly,
					IsDetailIntensityApplied = eq.IsDetailIntensityApplied,
				});

				var intStr = eq.Intensity.ToShortString().Replace('*', '-');
				if (
					(!eq.IsTraining || !UpdatedTrainingSound.Play(new() { { "int", intStr } })) &&
					(eq.Intensity == prevInt || !IntensityUpdatedSound.Play(new() { { "int", intStr } }))
				)
					UpdatedSound.Play(new() { { "int", intStr } });
			}))
			.Concat()
			.Subscribe(_ => { }, ex => Logger.LogError(ex, "地震情報更新処理中に例外が発生しました"));

		Service.Failed
			.ObserveOn(RxSchedulers.MainThreadScheduler)
			.Subscribe(_ =>
			{
				IsFault = true;
				IsLoading = false;
			});

		RegisterSystemWorkflows();
	}

	public async Task Restart()
	{
		IsFault = false;
		IsLoading = true;
		await TelegramProvideService.RestoreAsync();
	}

	public override Size MinViewSize { get; } = new(800, 600);

	private EarthquakeView? _control;
	public override Control DisplayControl => _control ?? throw new InvalidOperationException("初期化前にコントロールが呼ばれています");
	public override ISettingPage[] SettingPages => [
		new BasicSettingPage<EarthquakePage>("\xf05a", "地震情報", []),
	];

	public override void Initialize()
	{
		MessageBus.Current.Listen<ProcessJmaEqdbRequested>().Subscribe(async x => await ProcessJmaEqdbAsync(x.Id));
		MessageBus.Current.Listen<MapLoaded>().Subscribe(x => MapData = x.Data);
	}

	public override void RecreateDisplayControl()
	{
		_control = new EarthquakeView { DataContext = this };
		// if (Service.Earthquakes.Count > 0 && !IsLoading)
		// 	ProcessEarthquakeEvent(Service.Earthquakes[0]).ConfigureAwait(false);
	}

	public async Task OpenXml()
	{
		try
		{
			if (_control == null || Service == null || KyoshinEewViewerApp.TopLevelControl is not { } tlc)
				return;
			var files = await tlc.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
			{
				Title = "任意のXML電文を開く",
				FileTypeFilter = [
					FilePickerFileTypes.All,
				],
				AllowMultiple = false,
			});
			if (files is not { Count: > 0 } || !files[0].Name.EndsWith(".xml"))
				return;
			if (await Service.ProcessInformation(new FakeTelegram(files[0]), true) is { } eq)
				await ProcessEarthquakeEvent(eq);
			TelegramProcessError = null;
		}
		catch (Exception ex)
		{
			Logger?.LogWarning(ex, "外部XMLの読み込みに失敗しました");

			TelegramProcessError = ex.Message;
			ResetView();
		}
	}
	private class FakeTelegram(IStorageFile file) : Telegram("", "", file.Name, DateTime.Now)
	{
		public override void Cleanup() { }
		public override Task<Stream> GetBodyAsync() => file.OpenReadAsync();
	}

	/// <summary>
	/// 地震情報一覧からの選択処理
	/// </summary>
	/// <param name="eq">選ばれた項目</param>
	/// <returns></returns>
	private async Task ProcessEarthquakeEvent(EarthquakeEvent eq)
	{
		if (_control == null || Service == null)
			return;
		foreach (var e in Service.Earthquakes.ToArray())
			if (e != null)
				e.IsSelecting = e == eq;
		CurrentEvent = eq;

		try
		{
			ResetView();

			// TODO 電文を選べるようにする
			var lastFragment = eq.Fragments.LastOrDefault(f => f is IntensityInformationFragment or HypocenterAndIntensityInformationFragment and not LpgmIntensityInformationFragment)
				?? eq.Fragments.LastOrDefault();
			if (lastFragment != null)
			{
				await ProcessInformationFragment(eq, lastFragment);
				TelegramProcessError = null;
			}
		}
		catch (Exception ex)
		{
			TelegramProcessError = ex.Message;
			ResetView();
			Logger.LogError(ex, "表示のための電文の読み込みに失敗しました");
		}
	}

	private void ResetView()
	{
		EarthquakeLayer.ClearPoints();
		MapDisplayParameter = MapDisplayParameter with { CustomColorMap = null };
		MapNavigationRequest = null;
		ObservationIntensityGroups = null;
	}

	//public ReactiveCommand<string, Unit> ProcessHistoryXml { get; }

	// 仮 内部でbodyはdisposeします
	private async Task ProcessInformationFragment(EarthquakeEvent evt, EarthquakeInformationFragment targetFragment)
	{
		var zoomPoints = new List<Location>();
		var hypocenters = new List<(Location Location, Location? Error)>();
		var areaItems = new Dictionary<JmaIntensity, List<(Location Location, string Name)>>();
		var cityItems = new Dictionary<JmaIntensity, List<(Location Location, string Name)>>();
		var stationItems = new Dictionary<JmaIntensity, List<(Location Location, string Name)>>();

		// 震源に関する情報を解析する XMLからは処理しない
		Location? ProcessHypocenter()
		{
			if (evt?.Location == null)
				return null;

			hypocenters.Add((evt.Location, evt.LocationError));
			return evt.Location;
		}

		// 観測情報が存在する情報の場合読み込む
		if (targetFragment is IntensityInformationFragment or HypocenterAndIntensityInformationFragment)
		{
			var colorMap = new Dictionary<LandLayerType, Dictionary<int, SKColor>>();
			var pointGroups = new List<ObservationIntensityGroup>();

			await using var stream = await targetFragment.BasedTelegram.GetBodyAsync();
			using var report = new JmaXmlDocument(stream);

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
								foreach (var cityPoly in cityLayer.FindPolygon(city.Code))
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
								foreach (var p in areaLayer.FindPolygon(area.Code))
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

			switch (report.Control.Title)
			{
				case "震源・震度に関する情報":
					ProcessDetailPoints(false);
					break;
				case "震度速報":
					ProcessDetailPoints(true);
					break;
			}

			MapDisplayParameter = MapDisplayParameter with { CustomColorMap = colorMap };
			ObservationIntensityGroups = pointGroups.OrderByDescending(g => g.Intensity switch { JmaIntensity.Unknown => (((int)JmaIntensity.Int5Lower) * 10) - 1, _ => ((int)g.Intensity) * 10 }).ToArray();
		}

		// 震央座標を取得して描画優先度が震央に近い順になるようにソート
		var hypocenter = ProcessHypocenter();
		if (hypocenter != null)
		{
			SortItems(hypocenter, areaItems);
			SortItems(hypocenter, cityItems);
			SortItems(hypocenter, stationItems);

			// 地震の規模に応じて表示範囲を変更する
			var size = .1f;
			if (evt.Magnitude >= 4)
				size = .3f;
			if ((evt.Magnitude >= 6 && evt.IsForeign) || evt.IsVolcano)
				size = 30;

			zoomPoints.Add(new Location(hypocenter.Latitude - size, hypocenter.Longitude - size));
			zoomPoints.Add(new Location(hypocenter.Latitude + size, hypocenter.Longitude + size));
		}
		EarthquakeLayer.UpdatePoints(hypocenters, areaItems, cityItems.Count != 0 ? cityItems : null, stationItems.Count != 0 ? stationItems : null);

		// 自動ズーム範囲を計算
		if (zoomPoints.Count <= 0)
			return;

		MapNavigationRequest = new(zoomPoints.CalcRect());
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
			var hypocenters = new List<(Location Location, Location? Error)>();

			EarthquakeEvent? eq = null;

			if (data.Res.HypoCenters == null)
				throw new EarthquakeTelegramParseException("震源情報が見つかりません");

			// 震源情報をセット
			foreach (var hypo in Enumerable.Reverse(data.Res.HypoCenters))
			{
				eq ??= new EarthquakeEvent(hypo.Id ?? "");

				if (hypo.Location == null)
					continue;

				eq.Place = hypo.Name;
				if (!DateTime.TryParse(hypo.OccurrenceTime, out var ot))
					throw new EarthquakeTelegramParseException("日付がパースできません");
				eq.Time = ot;
				eq.Location = hypo.Location;
				eq.Intensity = hypo.MaxIntensity;
				eq.Depth = hypo.DepthKm ?? throw new EarthquakeTelegramParseException("震源の深さが取得できません");
				eq.Comment = "出典: 気象庁 震度データベース";
				if (float.TryParse(hypo.Magnitude, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var magnitude))
					eq.Magnitude = magnitude;
				else
					eq.MagnitudeAlternativeText = hypo.Magnitude;

				hypocenters.Add((hypo.Location, null));
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

			var hcLoc = hypocenters.Count > 0 ? hypocenters[^1].Location : null;
			if (hcLoc != null)
				SortItems(hcLoc, stationItems);


			if (zoomPoints.Count != 0)
			{
				MapNavigationRequest = new(zoomPoints.CalcRect());
			}

			CurrentEvent = eq;
			EarthquakeLayer.UpdatePoints(hypocenters, null, null, stationItems);
			ObservationIntensityGroups = pointGroups.OrderByDescending(g => g.Intensity switch { JmaIntensity.Unknown => (((int)JmaIntensity.Int5Lower) * 10) - 1, _ => ((int)g.Intensity) * 10 }).ToArray();
			TelegramProcessError = null;
		}
		catch (Exception ex)
		{
			TelegramProcessError = ex.Message;
			EarthquakeLayer.ClearPoints();
			ObservationIntensityGroups = null;
		}

		MapDisplayParameter = MapDisplayParameter with { CustomColorMap = null };
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
			this.RaiseAndSetIfChanged(ref _isHistoryShown, value);
			MapDisplayParameter = MapDisplayParameter with { Padding = new(MapDisplayParameter.Padding.Left, MapDisplayParameter.Padding.Top, value ? 240 : 0, MapDisplayParameter.Padding.Bottom) };
			Config.Earthquake.ShowHistory = value;
		}
	}

	private EarthquakeEvent? _currentEvent;
	public EarthquakeEvent? CurrentEvent
	{
		get => _currentEvent;
		set {
			if (_currentEvent == value)
				return;
			if (_currentEvent != null)
				_currentEvent.IsSelecting = false;
			this.RaiseAndSetIfChanged(ref _currentEvent, value);
			if (_currentEvent == null)
			{
				ResetView();
				RemarksIntensities = null;
				return;
			}
			if (!_currentEvent.IsSelecting)
				ProcessEarthquakeEvent(_currentEvent).ConfigureAwait(false);
			_currentEvent.IsSelecting = true;

			// 震度2以上の時のみ凡例を表示させる
			if (_currentEvent.Intensity > JmaIntensity.Int1)
				RemarksIntensities = Enumerable.Range((int)JmaIntensity.Int1, (int)_currentEvent.Intensity - 1).Reverse().Cast<JmaIntensity>().ToArray();
			else
				RemarksIntensities = null;
		}
	}

	private JmaIntensity[]? _remarksIntensities;
	public JmaIntensity[]? RemarksIntensities
	{
		get => _remarksIntensities;
		set => this.RaiseAndSetIfChanged(ref _remarksIntensities, value);
	}

	private string? _telegramProcessError;
	public string? TelegramProcessError
	{
		get => _telegramProcessError;
		set => this.RaiseAndSetIfChanged(ref _telegramProcessError, value);
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

	private void RegisterSystemWorkflows()
	{
		// 地震情報更新通知のSystemWorkflow
		var updateWorkflow = new WorkflowsNamespace.Workflow
		{
			Name = "System: 地震情報更新通知",
			Trigger = new EarthquakeInformationTrigger
			{
				Intensity = JmaIntensity.Unknown,
				IsIntensityChangeOnly = false,
				EnableSokuhou = true,
				EnableEpicenter = true,
				EnableDetail = true,
				EnableUpdateEpicenter = true,
				EnableTsunami = true,
				EnableLpgm = true
			},
			Actions = new MultipleAction
			{
				ChildActions =
				{
					new ChildAction
					{
						Action = new SendNotificationAction
						{
							Title = EarthquakeNotificationTemplates.NotificationTitle,
							TemplateText = EarthquakeNotificationTemplates.NotificationMessage
						}
					}
				}
			}
		};

		Config.WhenAnyValue(x => x.Notification.GotEq)
			.Subscribe(enabled => updateWorkflow.Enabled = enabled);

		WorkflowService.SystemWorkflows.Add(updateWorkflow);

		// 地震情報更新時のタブ切り替えSystemWorkflow
		var switchWorkflow = new WorkflowsNamespace.Workflow
		{
			Name = "System: 地震情報更新時タブ切り替え",
			Trigger = new EarthquakeInformationTrigger
			{
				Intensity = JmaIntensity.Unknown,
				IsIntensityChangeOnly = false,
				EnableSokuhou = true,
				EnableEpicenter = true,
				EnableDetail = true,
				EnableUpdateEpicenter = true,
				EnableTsunami = true,
				EnableLpgm = true
			},
			Actions = new MultipleAction
			{
				ChildActions =
				{
					new ChildAction { Action = new SwitchTabAction() }
				}
			}
		};

		Config.WhenAnyValue(x => x.Earthquake.SwitchAtUpdate)
			.Subscribe(enabled => switchWorkflow.Enabled = enabled);

		WorkflowService.SystemWorkflows.Add(switchWorkflow);
	}

	public void OpenTimetableWindow()
	{
		if (_control == null)
			return;

		var timeTables = new List<EarthquakeTimetable>();

		foreach (var g in Service.Earthquakes.GroupBy(eq => eq.Time.Date).OrderBy(g => g.Key))
		{
			var tables = new List<EarthquakeTimetableEntry>();
			foreach (var b in g.GroupBy(eq => eq.Time.Hour).OrderBy(b => b.Key))
				tables.Add(new EarthquakeTimetableEntry(b.First().Time, b.OrderBy(eq => eq.Time).Where(eq => !eq.IsCancelled && !eq.IsVolcano && eq.IsDetailIntensityApplied).ToArray()));

			// 抜けている時間があれば空の配列を突っ込む
			for (var i = 0; i < 24; i++)
			{
				if (!tables.Any(t => t.Time.Hour == i))
				{
					var time = new DateTime(g.Key.Year, g.Key.Month, g.Key.Day, i, 0, 0);
					if (time <= DateTime.Now)
						tables.Add(new EarthquakeTimetableEntry(time, []));
				}
			}

			if (tables.Count > 0)
				timeTables.Add(new EarthquakeTimetable(g.Key, tables.OrderBy(t => t.Time).ToArray()));
		}


		var window = new EarthquakeTimetableWindow
		{
			Timetables = timeTables.ToArray(),
		};
		if (KyoshinEewViewerApp.TopLevelControl is Window w)
			window.ShowDialog(w);
	}
}
