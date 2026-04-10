using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using DmdataSharp.ApiResponses.V2.Parameters;
using DmdataSharp.Exceptions;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Events;
using KyoshinEewViewer.JmaXmlParser;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Data;
using KyoshinEewViewer.Series.Earthquake;
using KyoshinEewViewer.Series.Tsunami.Events;
using KyoshinEewViewer.Series.Tsunami.MapLayers;
using KyoshinEewViewer.Series.Tsunami.Models;
using KyoshinEewViewer.Series.Tsunami.SettingPages;
using KyoshinEewViewer.Series.Tsunami.Templates;
using KyoshinEewViewer.Series.Tsunami.Workflow;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.Services.TelegramPublishers.Dmdata;
using KyoshinEewViewer.Services.Workflows.BuiltinActions;
using WorkflowsNamespace = KyoshinEewViewer.Services.Workflows;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Location = KyoshinMonitorLib.Location;

namespace KyoshinEewViewer.Series.Tsunami;
public class TsunamiSeries : SeriesBase
{
	public static SeriesMeta MetaData { get; } = new(typeof(TsunamiSeries), "tsunami", "津波情報", "\xe515", true, "津波情報を表示します。");

	private bool IsInitializing { get; set; }
	private ILogger Logger { get; set; }
	public KyoshinEewViewerConfiguration Config { get; }
	public TelegramProvideService TelegramProvider { get; }
	public NotificationService NotificationService { get; }
	public WorkflowService WorkflowService { get; }
	public TsunamiBorderLayer TsunamiBorderLayer { get; }
	// public TsunamiStationLayer TsunamiStationLayer { get; }
	private MapData? MapData { get; set; }

	public TsunamiStationParameterResponse? Stations { get; private set; }

	/// <summary>
	/// 期限切れの情報を揮発させるタイマー
	/// </summary>
	public Timer? ExpireTimer { get; set; }

	private static readonly string[] SupportedControlTitle = ["津波警報・注意報・予報a", "津波情報a"];

	private SoundCategory SoundCategory { get; } = new("Tsunami", "津波情報");
	private Sound? NewSound { get; set; }
	private Sound? UpdatedSound { get; set; }
	private Sound? UpgradeSound { get; set; }
	private Sound? DowngradeSound { get; set; }

	public TsunamiSeries(
		ILogManager logManager,
		KyoshinEewViewerConfiguration config,
		TelegramProvideService telegramProvider,
		NotificationService notificationService,
		SoundPlayerService soundPlayer,
		TimerService timerService,
		DmdataRedundantTelegramPublisher dmdata,
		WorkflowService workflowService
	) : base(MetaData)
	{
		SplatRegistrations.RegisterLazySingleton<TsunamiSeries>();

		Logger = logManager.GetLogger<TsunamiSeries>();
		Config = config;
		TelegramProvider = telegramProvider;
		NotificationService = notificationService;
		WorkflowService = workflowService;

		TsunamiBorderLayer = new TsunamiBorderLayer();
		// TsunamiStationLayer = new TsunamiStationLayer();
		MessageBus.Current.Listen<MapLoaded>().Subscribe(e => MapData = TsunamiBorderLayer.Map = e.Data);
		MapDisplayParameter = new MapDisplayParameter
		{
			BackgroundLayers = [TsunamiBorderLayer],
			// OverlayLayers = [TsunamiStationLayer],
			LayerSets = [
				new(0, LandLayerType.EarthquakeInformationPrefecture),
			],
		};

		NewSound = soundPlayer.RegisterSound(SoundCategory, "New", "津波情報の発表", "未発表状態から受信した際に鳴動します。\n{lv}: 警報種別 [fore, adv, warn, major]", new() { { "lv", "fore" }, });
		UpgradeSound = soundPlayer.RegisterSound(SoundCategory, "Upgrade", "警報/注意報の更新", "より上位の警報/注意報が発表された際に鳴動します。\n{lv}: 更新後の警報種別 [fore, adv, warn, major]", new() { { "lv", "warn" }, });
		DowngradeSound = soundPlayer.RegisterSound(SoundCategory, "Downgrade", "警報/注意報の解除", "最大の警報レベルが下がった時に鳴動します。\n{lv}: 解除後の警報種別 [none, fore, adv, warn, major]", new() { { "lv", "none" }, });
		UpdatedSound = soundPlayer.RegisterSound(SoundCategory, "Updated", "津波情報の更新", "他の津波関連の音声が再生されなかった場合、この音声が鳴動します。\n{lv}: 最大の警報種別 [fore, adv, warn, major]", new() { { "lv", "adv" }, });

		ExpireTimer = new Timer(_ =>
		{
			if (Current?.CheckExpired(timerService.CurrentTime) ?? false)
			{
				Current = null;
				MapNavigationRequest = null;
			}
		});

		TelegramProvider.Subscribe(
			InformationCategory.Tsunami,
			async (s, t) =>
			{
				IsInitializing = true;

				if (s.Contains("DM-D.S.S") && Stations == null)
					try
					{
						Stations = await dmdata.GetTsunamiStationsAsync();
					}
					catch (DmdataForbiddenException) { }
					catch (Exception ex)
					{
						Logger.LogError(ex, "津波観測点情報取得中に問題が発生しました");
					}

				try
				{
					SourceName = s;

					// 最後の津波予報から順番に処理
					var last = t.LastOrDefault(t => t.Title == "津波警報・注意報・予報a");
					if (last == null)
						return;
					foreach (var lt in t.Skip(Array.IndexOf(t, last)).Where(t => SupportedControlTitle.Contains(t.Title)).ToArray())
					{
						await using var stream = await lt.GetBodyAsync();
						using var report = new JmaXmlDocument(stream);
						(var tsunami, var bound) = ProcessInformation(report);
						if (tsunami == null || tsunami.CheckExpired(timerService.CurrentTime))
							return;
						Current = tsunami;
						MapNavigationRequest = new(bound);
					}
				}
				finally
				{
					IsInitializing = false;
				}
			},
			async t =>
			{
				if (!SupportedControlTitle.Contains(t.Title))
					return;
				await using var stream = await t.GetBodyAsync();
				using var report = new JmaXmlDocument(stream);
				(var tsunami, var bound) = ProcessInformation(report);
				if (tsunami == null || (Current != null && tsunami.ReportedAt <= Current.ReportedAt) || tsunami.CheckExpired(timerService.CurrentTime))
					return;
				Current = tsunami;
				MapNavigationRequest = new(bound);
			},
			s =>
			{
				SourceName = null;
				// TODO: 状態管理はもうちょっとちゃんとやる必要がある
			}
		);

		if (Design.IsDesignMode)
		{
			SourceName = "Source";
			Current = new TsunamiInfo()
			{
				SpecialState = "テスト",
				ReportedAt = DateTime.Now,
				ExpireAt = DateTime.Now.AddHours(2),
				MajorWarningAreas = [
					new(0, "地域A", "10m超", "ただちに津波襲来") { ArrivalTime = DateTime.Now},
					new(0, "地域B", "10m超", "第1波到達を確認") { ArrivalTime = DateTime.Now},
					new(0, "地域C", "10m", "14:30 到達見込み") { ArrivalTime = DateTime.Now},
					new(0, "地域D", "巨大", "14:45") { ArrivalTime = DateTime.Now},
					new(0, "あまりにも長すぎる名前の地域", "ながい", "あまりにも長すぎる説明") { ArrivalTime = DateTime.Now},
				],
				WarningAreas = [
					new(0, "地域E", "高い", "14:30") { ArrivalTime = DateTime.Now},
				],
				AdvisoryAreas = [
					new(0, "地域F", "1m", "14:30") { ArrivalTime = DateTime.Now},
					new(0, "地域G", "", "14:30") { ArrivalTime = DateTime.Now},
				],
				ForecastAreas = [
					new(0, "地域H", "0.2m未満", "") { ArrivalTime = DateTime.Now},
				],
			};
			return;
		}

		ExpireTimer.Change(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
		
		RegisterSystemWorkflows();
	}

	private TsunamiView? _control;
	public override Control DisplayControl => _control ?? throw new InvalidOperationException("初期化前にコントロールが呼ばれています");
	public override ISettingPage[] SettingPages => [
		new BasicSettingPage<TsunamiPage>("\xe515", "津波情報", []),
	];

	private string? _sourceName;
	/// <summary>
	/// 情報の受信元
	/// </summary>
	public string? SourceName
	{
		get => _sourceName;
		set => this.RaiseAndSetIfChanged(ref _sourceName, value);
	}

	private bool _isFault;
	public bool IsFault
	{
		get => _isFault;
		set => this.RaiseAndSetIfChanged(ref _isFault, value);
	}

	public bool IsDebugBuiid =>
#if DEBUG
		true;
#else
		false;
#endif

	private TsunamiInfo? _current;
	public TsunamiInfo? Current
	{
		get => _current;
		set {
			if (_current != value && !IsInitializing)
			{
				var level = value?.Level switch
				{
					TsunamiLevel.MajorWarning => "major",
					TsunamiLevel.Warning => "warn",
					TsunamiLevel.Advisory => "adv",
					TsunamiLevel.Forecast => "fore",
					_ => "none",
				};
				var levelStr = value?.Level switch
				{
					TsunamiLevel.MajorWarning => "大津波警報",
					TsunamiLevel.Warning => "津波警報",
					TsunamiLevel.Advisory => "津波注意報",
					TsunamiLevel.Forecast => "津波予報",
					_ => "",
				};

				// 発表
				if (
					(_current == null || _current.Level <= TsunamiLevel.None) && value != null &&
					(
						value.AdvisoryAreas != null ||
						value.ForecastAreas != null ||
						value.MajorWarningAreas != null ||
						value.WarningAreas != null
					)
				)
				{
					if (!NewSound?.Play(new Dictionary<string, string> { { "lv", level } }) ?? false)
						UpdatedSound?.Play(new Dictionary<string, string> { { "lv", level } });
				}
				// 切り替え(解除)
				else if (_current != null && _current.Level > TsunamiLevel.None && (value == null || value.Level < _current.Level))
				{
					if (!DowngradeSound?.Play(new Dictionary<string, string> { { "lv", level } }) ?? false)
						UpdatedSound?.Play(new Dictionary<string, string> { { "lv", level } });
				}
				// 切り替え(上昇)
				else if (_current != null && value != null && _current.Level < value.Level)
				{
					if (!UpgradeSound?.Play(new Dictionary<string, string> { { "lv", level } }) ?? false)
						UpdatedSound?.Play(new Dictionary<string, string> { { "lv", level } });
				}
				else
				{
					UpdatedSound?.Play(new Dictionary<string, string> { { "lv", level } });
				}
				MessageBus.Current.SendMessage(new TsunamiInformationUpdated(_current, value));
				WorkflowService?.PublishEvent(new TsunamiInformationEvent(this)
				{
					TsunamiInfo = value,
					PreviousLevel = _current?.Level ?? TsunamiLevel.None,
				});
			}
			// 予報の場合は期限を引き継ぐ
			if (value != null && _current?.EventId == value.EventId && value.Level == TsunamiLevel.Forecast && _current?.ExpireAt != null && value.ExpireAt == null)
				value.ExpireAt = _current.ExpireAt;

			this.RaiseAndSetIfChanged(ref _current, value);

			if (TsunamiBorderLayer != null)
				TsunamiBorderLayer.Current = value;
			//if (TsunamiStationLayer != null)
			//	TsunamiStationLayer.Current = value;
			MapDisplayParameter = MapDisplayParameter with { Padding = new(_current == null ? 0 : 360, 0, 0, 0) };
		}
	}

	public override Size MinViewSize { get; } = new(650, 500);

	public override void RecreateDisplayControl()
		=> _control = new TsunamiView { DataContext = this };

	public async Task Restart()
	{
		IsFault = false;
		SourceName = null;
		await TelegramProvider.RestoreAsync();
	}

	public async Task OpenXml()
	{
		if (TopLevel.GetTopLevel(_control) is not { } topLevel)
			return;

		try
		{
			var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
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

			await using var stream = await files[0].OpenReadAsync();
			using var report = new JmaXmlDocument(stream);
			(Current, var focus) = ProcessInformation(report);
			MapNavigationRequest = new(focus);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "外部XMLの読み込みに失敗しました");
		}
	}

	public (TsunamiInfo?, Avalonia.Rect?) ProcessInformation(JmaXmlDocument report)
	{
		if (!SupportedControlTitle.Contains(report.Control.Title))
			return (null, null);
		var tsunami = new TsunamiInfo
		{
			EventId = report.Head.EventId
		};
		if (report.Control.Status != "通常")
			tsunami.SpecialState = report.Control.Status;
		tsunami.ReportedAt = report.Head.ReportDateTime.DateTime;
		tsunami.ExpireAt = report.Head.ValidDateTime?.DateTime;

		var zoomPoints = new List<Location>();
		FeatureLayer? tsunamiLayer = null;
		MapData?.TryGetLayer(LandLayerType.TsunamiForecastArea, out tsunamiLayer);

		var forecastAreas = new List<TsunamiWarningArea>();
		var advisoryAreas = new List<TsunamiWarningArea>();
		var warningAreas = new List<TsunamiWarningArea>();
		var majorWarningAreas = new List<TsunamiWarningArea>();
		var noTsunamiAreas = new List<TsunamiWarningArea>();

		var tsunamiForecast = report.TsunamiBody.Tsunami?.Forecast ?? throw new Exception("Body/Tsunami/Forecast がみつかりません");
		foreach (var i in tsunamiForecast.Items)
		{
			if (i.Category.Kind.Code
				is "00" // 津波なし
				or "50" // 警報解除
				or "60" // 注意報解除
			)
				continue;

			var height = i.MaxHeight?.TsunamiHeight?.Description;
			// 津波予報時かつ高さの情報がない場合は海面変動の文言を入れる
			if (height == null && i.Category.Kind.Code is "71" or "72" or "73")
				height = "若干の海面変動";
			var area = new TsunamiWarningArea(
				i.Area.Code,
				i.Area.Name,
				Utils.ConvertToShortWidthString(height ?? throw new Exception("TsunamiHeight/Description がみつかりません")),
				Utils.ConvertToShortWidthString(i.FirstHeight?.ArrivalTime?.ToString("HH:mm 到達見込み") ?? i.FirstHeight?.Condition ?? "") // 予報の場合は要素が存在しないので空文字に
			)
			{
				ArrivalTime = i.FirstHeight?.ArrivalTime?.DateTime ?? // 到達時刻はソートに使用するため Condition に応じて暫定値を入れる
					(i.FirstHeight?.Condition == "ただちに津波来襲と予測" ? tsunami.ReportedAt.AddHours(-1) : (DateTime?)null) ??
					(i.FirstHeight?.Condition == "津波到達中と推測" ? tsunami.ReportedAt.AddHours(-2) : (DateTime?)null) ??
					(i.FirstHeight?.Condition == "第１波の到達を確認" ? tsunami.ReportedAt.AddHours(-3) : (DateTime?)null) ??
					tsunami.ReportedAt // 算出できなければ電文の作成時刻を入れる
			};

			if (i.Category.Kind.Code is "51") // 警報
				warningAreas.Add(area);
			else if (i.Category.Kind.Code is "52" or "53") // 大津波警報
				majorWarningAreas.Add(area);
			else if (i.Category.Kind.Code is "62") // 注意報
				advisoryAreas.Add(area);
			else if (i.Category.Kind.Code is "71" or "72" or "73") // 予報
				forecastAreas.Add(area);

			var stations = new List<TsunamiObservationStation>();
			foreach (var s in i.Stations)
			{
				var dmdataStation = Stations?.Items?.FirstOrDefault(i => i.Code == s.Code.ToString());
				stations.Add(new(s.Code, s.Name, dmdataStation?.Kana, dmdataStation?.GetLocation()
				)
				{
					ArrivalTime = s.FirstHeight?.ArrivalTime?.DateTime ?? // 到達時刻はソートに使用するため Condition に応じて暫定値を入れる
						(s.FirstHeight?.Condition == "ただちに津波来襲と予測" ? tsunami.ReportedAt.AddHours(-1) : (DateTime?)null) ??
						(s.FirstHeight?.Condition == "津波到達中と推測" ? tsunami.ReportedAt.AddHours(-2) : (DateTime?)null) ??
						(s.FirstHeight?.Condition == "第１波の到達を確認" ? tsunami.ReportedAt.AddHours(-3) : (DateTime?)null) ??
						tsunami.ReportedAt, // 算出できなければ電文の作成時刻を入れる,
					HighTideTime = s.HighTideDateTime,
					FirstHeight = Utils.ConvertToShortWidthString(i.MaxHeight?.TsunamiHeight?.Description ?? ""),
					FirstHeightDetail = Utils.ConvertToShortWidthString(s.FirstHeight?.ArrivalTime?.ToString("HH:mm 到達見込み") ?? s.FirstHeight?.Condition ?? ""),
				});
			}
			if (stations.Count > 0)
				area.Stations = stations.OrderBy(a => a.ArrivalTime).ToArray();

			if (tsunamiLayer != null)
			{
				foreach (var p in tsunamiLayer.FindPolygon(i.Area.Code))
				{
					zoomPoints.Add(p.BoundingBox.TopLeft.CastLocation());
					zoomPoints.Add(p.BoundingBox.BottomRight.CastLocation());
				}
			}
		}

		if (report.TsunamiBody.Tsunami?.Observation is { } tsunamiObservation)
		{
			foreach (var i in tsunamiObservation.Items)
			{
				var area = majorWarningAreas.FirstOrDefault(a => a.Code == i.Area.Code) ??
					warningAreas.FirstOrDefault(a => a.Code == i.Area.Code) ??
					advisoryAreas.FirstOrDefault(a => a.Code == i.Area.Code) ??
					forecastAreas.FirstOrDefault(a => a.Code == i.Area.Code) ??
					noTsunamiAreas.FirstOrDefault(a => a.Code == i.Area.Code);

				if (area == null)
				{
					area = new TsunamiWarningArea(
						i.Area.Code,
						i.Area.Name,
						"",
						""
					)
					{
						ArrivalTime = tsunami.ReportedAt
					};
					noTsunamiAreas.Add(area);
				}

				var stations = new List<TsunamiObservationStation>(area.Stations ?? []);
				foreach (var s in i.Stations)
				{
					var station = stations.FirstOrDefault(st => st.Code == s.Code);
					if (station == null)
					{
						var dmdataStation = Stations?.Items?.FirstOrDefault(i => i.Code == s.Code.ToString());
						stations.Add(station = new(s.Code, s.Name, dmdataStation?.Kana, dmdataStation?.GetLocation()) { ArrivalTime = tsunami.ReportedAt });
					}
					station.MaxHeight = s.MaxHeight?.TsunamiHeight?.TryGetFloatValue(out var h) ?? false ? h : null;
					station.MaxHeightTime = s.MaxHeight?.DateTime;
					station.MaxHeightDetail = (s.MaxHeight?.TsunamiHeight == null ? s.MaxHeight?.Condition : null) ?? "";
					station.IsOutRange = s.MaxHeight?.TsunamiHeight?.Description?.EndsWith("以上") ?? false;
					station.IsRising = s.MaxHeight?.Condition == "増加中";
				}

				if (stations.Count > 0)
					area.Stations = stations.OrderBy(a => a.ArrivalTime).ThenByDescending(s => s.MaxHeight ?? float.MinValue).ToArray();
			}
		}

		TsunamiWarningArea[] SortAreas(List<TsunamiWarningArea> areas)
			=> areas.OrderBy(a => a.ArrivalTime).ThenBy(a => a.Height switch
			{
				"10m超" => -4,
				"10m" => -3,
				"5m" => -2,
				"3m" => -1,
				_ => 0,
			}).ToArray();

		if (forecastAreas.Count > 0)
			tsunami.ForecastAreas = SortAreas(forecastAreas);
		if (advisoryAreas.Count > 0)
			tsunami.AdvisoryAreas = SortAreas(advisoryAreas);
		if (warningAreas.Count > 0)
			tsunami.WarningAreas = SortAreas(warningAreas);
		if (majorWarningAreas.Count > 0)
			tsunami.MajorWarningAreas = SortAreas(majorWarningAreas);
		if (noTsunamiAreas.Count > 0)
			tsunami.NoTsunamiAreas = SortAreas(noTsunamiAreas);

		if (zoomPoints.Count != 0)
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
			return (tsunami, new Avalonia.Rect(minLat - 1, minLng - 1, maxLat - minLat + 2, maxLng - minLng + 3));
		}

		return (tsunami, null);
	}

	private void RegisterSystemWorkflows()
	{
		// 津波情報発表通知のSystemWorkflow
		var issuedWorkflow = new WorkflowsNamespace.Workflow
		{
			Name = "System: 津波情報発表通知",
			Trigger = new TsunamiInformationTrigger
			{
				EnableIssued = true,
				EnableUpgraded = false,
				EnableDowngraded = false,
				EnableUpdated = false
			},
			Actions = new MultipleAction
			{
				ChildActions =
				{
					new ChildAction
					{
						Action = new SendNotificationAction
						{
							Title = TsunamiNotificationTemplates.NotificationTitle,
							TemplateText = TsunamiNotificationTemplates.NotificationMessage
						}
					}
				}
			}
		};

		Config.WhenAnyValue(x => x.Notification.Tsunami)
			.Subscribe(enabled => issuedWorkflow.Enabled = enabled);

		WorkflowService.SystemWorkflows.Add(issuedWorkflow);

		// 津波情報解除・切り替え（下降）通知のSystemWorkflow
		var downgradeWorkflow = new WorkflowsNamespace.Workflow
		{
			Name = "System: 津波情報解除・切り替え通知",
			Trigger = new TsunamiInformationTrigger
			{
				EnableIssued = false,
				EnableUpgraded = false,
				EnableDowngraded = true,
				EnableUpdated = false
			},
			Actions = new MultipleAction
			{
				ChildActions =
				{
					new ChildAction
					{
						Action = new SendNotificationAction
						{
							Title = TsunamiNotificationTemplates.NotificationTitle,
							TemplateText = TsunamiNotificationTemplates.NotificationMessage
						}
					}
				}
			}
		};

		Config.WhenAnyValue(x => x.Notification.Tsunami)
			.Subscribe(enabled => downgradeWorkflow.Enabled = enabled);

		WorkflowService.SystemWorkflows.Add(downgradeWorkflow);

		// 津波情報切り替え（上昇）通知のSystemWorkflow
		var upgradeWorkflow = new WorkflowsNamespace.Workflow
		{
			Name = "System: 津波情報切り替え（上昇）通知",
			Trigger = new TsunamiInformationTrigger
			{
				EnableIssued = false,
				EnableUpgraded = true,
				EnableDowngraded = false,
				EnableUpdated = false
			},
			Actions = new MultipleAction
			{
				ChildActions =
				{
					new ChildAction
					{
						Action = new SendNotificationAction
						{
							Title = TsunamiNotificationTemplates.NotificationTitle,
							TemplateText = TsunamiNotificationTemplates.NotificationMessage
						}
					}
				}
			}
		};

		Config.WhenAnyValue(x => x.Notification.Tsunami)
			.Subscribe(enabled => upgradeWorkflow.Enabled = enabled);

		WorkflowService.SystemWorkflows.Add(upgradeWorkflow);

		// 津波情報更新通知のSystemWorkflow
		var updatedWorkflow = new WorkflowsNamespace.Workflow
		{
			Name = "System: 津波情報更新通知",
			Trigger = new TsunamiInformationTrigger
			{
				EnableIssued = false,
				EnableUpgraded = false,
				EnableDowngraded = false,
				EnableUpdated = true
			},
			Actions = new MultipleAction
			{
				ChildActions =
				{
					new ChildAction
					{
						Action = new SendNotificationAction
						{
							Title = TsunamiNotificationTemplates.NotificationTitle,
							TemplateText = TsunamiNotificationTemplates.NotificationMessage
						}
					}
				}
			}
		};

		Config.WhenAnyValue(x => x.Notification.Tsunami)
			.Subscribe(enabled => updatedWorkflow.Enabled = enabled);

		WorkflowService.SystemWorkflows.Add(updatedWorkflow);

		// 津波情報更新時のタブ切り替えSystemWorkflow（解除時以外）
		var switchWorkflow = new WorkflowsNamespace.Workflow
		{
			Name = "System: 津波情報更新時タブ切り替え",
			Trigger = new TsunamiInformationTrigger
			{
				EnableIssued = true,
				EnableUpgraded = true,
				EnableDowngraded = false,
				EnableUpdated = true
			},
			Actions = new MultipleAction
			{
				ChildActions =
				{
					new ChildAction { Action = new SwitchTabAction() }
				}
			}
		};

		Config.WhenAnyValue(x => x.Tsunami.SwitchAtUpdate)
			.Subscribe(enabled => switchWorkflow.Enabled = enabled);

		WorkflowService.SystemWorkflows.Add(switchWorkflow);
	}
}
