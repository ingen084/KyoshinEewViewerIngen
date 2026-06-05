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
	public static SeriesMeta MetaData { get; } = new(typeof(EarthquakeSeries), "earthquake", "地震情報", new FontIconSource { Glyph = "\xf05a", FontFamily = new(Utils.IconFontName) }, true, "震源･震度情報を受信･表示します。");

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
			.Subscribe(s =>
			{
				try
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
					ProcessEarthquakeEvent(Service.Earthquakes[0]);
				}
				catch (Exception ex)
				{
					Logger.LogError(ex, "受信元切替後の処理中に例外が発生しました");
				}
			});

		Service.EarthquakeUpdated
			.Where(u => !u.IsBulkInserting)
			.ObserveOn(RxSchedulers.MainThreadScheduler)
			.Subscribe(u =>
			{
				try
				{
					var eq = u.Earthquake;
					var fragment = u.Fragment;
					var prevInt = u.PreviousMaxIntensity;

					ProcessEarthquakeEvent(eq);
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
				}
				catch (Exception ex)
				{
					Logger.LogError(ex, "地震情報更新処理中に例外が発生しました");
				}
			});

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
	}

	public async Task OpenXml()
	{
		try
		{
			if (_control == null || Service == null)
				return;
			var files = await _control.GetTopLevel().StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
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
				ProcessEarthquakeEvent(eq);
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
	private void ProcessEarthquakeEvent(EarthquakeEvent eq)
	{
		if (_control == null || Service == null)
			return;
		foreach (var e in Service.Earthquakes.ToArray())
			if (e != null)
				e.IsSelecting = e == eq;
		CurrentEvent = eq;

		try
		{
			ApplyCurrentSnapshot(eq);
			TelegramProcessError = null;
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

	private void ApplyCurrentSnapshot(EarthquakeEvent evt)
	{
		var presentation = EarthquakeMapPresentationBuilder.Build(evt.CurrentSnapshot, MapData, Config.Earthquake);

		EarthquakeLayer.UpdatePoints(presentation.Hypocenters, presentation.AreaItems, presentation.CityItems, presentation.StationItems);
		// 色塗りエントリが無い場合は null を渡して旧 ResetView 経路と同じ挙動にする
		var hasFill = presentation.ColorMap.Values.Any(d => d.Count > 0);
		MapDisplayParameter = MapDisplayParameter with { CustomColorMap = hasFill ? presentation.ColorMap : null };
		MapNavigationRequest = presentation.AutoZoom is { } rect ? new MapNavigationRequest(rect) : null;
		ObservationIntensityGroups = presentation.Groups;
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

			if (data.Res.HypoCenters == null)
				throw new EarthquakeTelegramParseException("震源情報が見つかりません");

			EarthquakeEvent? eq = null;
			EarthquakeHypocenterSnapshot? hypocenterSnapshot = null;
			var maxIntensity = JmaIntensity.Unknown;

			foreach (var hypo in Enumerable.Reverse(data.Res.HypoCenters))
			{
				eq ??= new EarthquakeEvent(hypo.Id ?? "");

				if (hypo.Location == null)
					continue;

				if (!DateTime.TryParse(hypo.OccurrenceTime, out var ot))
					throw new EarthquakeTelegramParseException("日付がパースできません");

				var depthKm = hypo.DepthKm ?? throw new EarthquakeTelegramParseException("震源の深さが取得できません");
				float magnitudeValue = float.NaN;
				string? magnitudeAlt = null;
				if (float.TryParse(hypo.Magnitude, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var magnitude))
					magnitudeValue = magnitude;
				else
					magnitudeAlt = hypo.Magnitude;

				maxIntensity = hypo.MaxIntensity;
				hypocenterSnapshot = new EarthquakeHypocenterSnapshot(
					OriginTime: ot,
					Place: hypo.Name ?? "",
					Location: hypo.Location,
					LocationError: null,
					Magnitude: magnitudeValue,
					MagnitudeAlternativeText: magnitudeAlt,
					Depth: depthKm,
					DepthError: null,
					IsForeign: false,
					IsVolcano: false,
					VolcanoName: null);
			}
			if (eq == null || hypocenterSnapshot == null)
				throw new EarthquakeTelegramParseException("地震情報を組み立てることができませんでした");

			if (data.Res.IntensityStations == null)
				throw new EarthquakeTelegramParseException("震源情報が見つかりません");

			var stations = data.Res.IntensityStations
				.Where(st => st.Location != null)
				.Select(st => new StationIntensitySnapshot(
					Name: st.Name ?? "不明",
					Code: int.TryParse(st.Code, out var stCode) ? stCode : 0,
					Intensity: st.Intensity,
					MaxLpgmIntensity: null,
					LpgmByPeriod: null,
					Location: st.Location))
				.ToArray();

			var pseudoCity = new CityIntensitySnapshot("-", 0, maxIntensity, null, null, stations);
			var pseudoArea = new AreaIntensitySnapshot("-", 0, maxIntensity, null, null, [pseudoCity]);
			var pseudoPref = new PrefectureIntensitySnapshot("-", 0, maxIntensity, null, [pseudoArea]);

			var snapshot = new EarthquakeSnapshot(
				Scope: EarthquakeIntensityScope.Detail,
				Hypocenter: hypocenterSnapshot,
				MaxIntensity: maxIntensity,
				MaxLpgmIntensity: null,
				DetectionTime: null,
				SokuhouPlace: null,
				IsSingleArea: true,
				Comment: "出典: 気象庁 震度データベース",
				FreeFormComment: null,
				UpdatedTime: DateTime.Now,
				Prefectures: [pseudoPref]);

			eq.SetSnapshot(snapshot);
			CurrentEvent = eq;
			ApplyCurrentSnapshot(eq);
			TelegramProcessError = null;
		}
		catch (Exception ex)
		{
			TelegramProcessError = ex.Message;
			EarthquakeLayer.ClearPoints();
			ObservationIntensityGroups = null;
			MapDisplayParameter = MapDisplayParameter with { CustomColorMap = null };
		}
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
				ProcessEarthquakeEvent(_currentEvent);
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


	private IntensityViewGroup[]? _observationIntensityGroups;
	public IntensityViewGroup[]? ObservationIntensityGroups
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
