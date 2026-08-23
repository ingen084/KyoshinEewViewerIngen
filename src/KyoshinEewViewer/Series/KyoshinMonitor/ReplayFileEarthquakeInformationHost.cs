using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.EarthquakeReplay;
using KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;
using KyoshinEewViewer.Series.KyoshinMonitor.Services;
using KyoshinEewViewer.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.CustomControl;
using SkiaSharp;
using KyoshinEewViewer.JmaXmlParser;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinMonitorLib;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using KyoshinEewViewer.Core;

namespace KyoshinEewViewer.Series.KyoshinMonitor;

public partial class ReplayFileEarthquakeInformationHost : EarthquakeInformationHost
{
	private EewController EewController { get; set; }
	public EewPointForecastController PointForecastController { get; }
	private KyoshinMonitorWatchService KyoshinMonitorWatcher { get; }

	public bool IsRunning => Runner?.IsPlaying ?? false;

	[ObservableProperty]
	public partial ReplayFileHeader? CurrentHeader { get; set; }

	[ObservableProperty]
	public partial ReplayData[]? CurrentData { get; set; }

	[ObservableProperty]
	public partial ReplayFileHeader? LoadedHeader { get; set; }

	[ObservableProperty]
	public partial ReplayData[]? LoadedData { get; set; }

	private float _speedMultiplier = 1;
	public float SpeedMultiplier
	{
		get => _speedMultiplier;
		set {
			SetProperty(ref _speedMultiplier, value);
			if (Runner != null)
				Runner.SpeedMultiplier = value;
			ReplayDescription = $"リプレイファイル {SpeedMultiplier:0.0}倍速";
		}
	}

	public void ResetSpeedMultiplier()
	{
		SpeedMultiplier = 1;
	}

	private ReplayFileRunner? Runner { get; set; }

	private KyoshinEventStateTracker EventStateTracker { get; } = new();

	public override DateTime CurrentTime {
		get {
			var time = Runner?.CurrentTime ?? DateTime.Now;
			if (Config.Eew.SyncKyoshinMonitorPsWave)
				return time.AddSeconds(-1);
			return time;
		}
	}

	public ReplayFileEarthquakeInformationHost(
		ILogger<ReplayFileEarthquakeInformationHost> logger,
		KyoshinMonitorSeries series,
		KyoshinEewViewerConfiguration config,
		NotificationService notificationService,
		SoundPlayerService soundPlayer,
		WorkflowService workflowService,
		TimerService timerService,
		ObservationPointsUpdateService observationPointsUpdateService
	) : base(true, config)
	{
		EewController = new(AppLog.Create<EewController>(), series, config, soundPlayer, workflowService, isReplay: true);
		PointForecastController = new(AppLog.Create<EewPointForecastController>(), config, EewController, timerService, () => CurrentTime);
		EewController.EewUpdated += OnEewUpdated;
		KyoshinMonitorWatcher = new(AppLog.Create<KyoshinMonitorWatchService>(), Config, EewController, observationPointsUpdateService);
		KyoshinMonitorWatcher.RealtimeDataUpdated += OnRealtimeDataUpdated;
		KyoshinMonitorWatcher.WarningMessageUpdated += m => WarningMessage = m;
		KyoshinMonitorWatcher.RealtimeDataParseProcessStarted += t => IsWorking = true;

		// EEW受信
		EewController.EewUpdated += (time, eews) =>
		{
			Eews = eews.OrderByDescending(eew => eew.Hypocenter?.OccurrenceTime).ToArray();

			// 塗りつぶし地域組み立て
			var intensityAreas = eews.SelectMany(e => e.IntensityForecastMap ?? [])
				.GroupBy(p => p.Key, p => p.Value).ToDictionary(p => p.Key, p => p.Max());
			var warningAreaCodes = eews.SelectMany(e => e.WarningAreas?.Codes ?? []).Distinct().ToArray();
			if (Config.Eew.FillForecastIntensity && intensityAreas.Count != 0)
			{
				ShowIntensityColorSample = true;
				MapDisplayParameter = MapDisplayParameter with
				{
					CustomColorMap = new()
					{
						{
							LandLayerType.EarthquakeInformationSubdivisionArea,
							intensityAreas.ToDictionary(p => p.Key, p => FixedObjectRenderer.IntensityPaintCache[p.Value].Background.Color)
						},
					}
				};
			}
			else if (Config.Eew.FillWarningArea && warningAreaCodes.Length != 0)
			{
				ShowIntensityColorSample = false;
				MapDisplayParameter = MapDisplayParameter with
				{
					CustomColorMap = new()
					{
						{
							LandLayerType.EarthquakeInformationSubdivisionArea,
							warningAreaCodes.ToDictionary(c => c, c => SKColors.Tomato)
						},
					}
				};
			}
			else
			{
				ShowIntensityColorSample = false;
				MapDisplayParameter = MapDisplayParameter with { CustomColorMap = null };
			}

			UpateFocusPoint(time);
			OnEewUpdated(time, eews);
		};

		KyoshinMonitorWatcher.RealtimeDataUpdated += e =>
		{
			if (e.data != null)
				WarningMessage = null;
			IsWorking = false;
			// CurrentDisplayTime = e.time;
			KyoshinEvents = e.events;
			if (e.events.Length != 0)
			{
				foreach (var evt in e.events)
				{
					var result = EventStateTracker.CheckAndUpdate(evt);
					if (result.ShouldNotify)
						OnKyoshinEventUpdated((e.time, evt, result.IsLevelUp, result.IsRegionExpanded, result.IsSubRegionExpanded));
				}
				EventStateTracker.RemoveStaleEntries(e.events);

				// 揺れ検知地域を更新
				ShakeDetectedRegions = ShakeDetectedRegionBuilder.Build(e.events, RegionSubRegionMap);
				ShakeDetectedLevel = e.events.Max(ev => ev.Level);
			}
			else
			{
				ShakeDetectedRegions = [];
			}

			// 揺れ検知パネル表示判定: 通知レベル以上の場合のみ表示
			ShowShakeDetectedPanel = ShakeDetectedRegions.Length > 0 &&
				ShakeDetectedLevel >= Config.KyoshinMonitor.EventNotificationLevel;

			UpateFocusPoint(e.time);
			OnRealtimeDataUpdated(e);
		};
	}

	public async Task LoadAsync(string path)
	{
		using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
		var reader = new KyoshinReplayFileReader(stream);
		LoadedHeader = await reader.ReadHeader();
		LoadedData = await reader.ReadData(LoadedHeader.CompressionMode);
	}

	public void Start()
	{
		if (LoadedData == null)
			return;

		Runner?.StopAsync().ConfigureAwait(false);

		CurrentHeader = LoadedHeader;
		CurrentData = LoadedData;

		Runner = new ReplayFileRunner(CurrentData)
		{
			SpeedMultiplier = SpeedMultiplier,
		};
		Runner.DataArrived += (time, data) =>
		{
			// 毎秒ぴったりの場合はタイマーイベントを発生させる
			if (time.Millisecond == 0)
				EewController.TimerElapsed(time);

			// 強震モニタ
			string? eewJson = null;
			byte[]? imageBytes = null;

			foreach (var d in data)
			{
				switch (d)
				{
					case KyoshinMonitorImageReplayData img:
						img.Images.TryGetValue(KyoshinMonitorImageReplayData.ImageType.Shindo, out imageBytes);
						break;
					case KyoshinMonitorEewJsonReplayData eew:
						eewJson = eew.Json;
						break;
					case JmaXmlTelegramReplayData jma:
						ProcessJmaXmlEew(jma.Telegram, time);
						break;
					case KEViJsonReplayData kevi:
						switch (kevi.Type)
						{
							case KEViJsonReplayData.JsonType.Eew:
								var eew = JsonSerializer.Deserialize<Eew>(kevi.Json);
								if (eew != null)
									EewController.Update(eew, time);
								break;
							case KEViJsonReplayData.JsonType.EewWarning:
								var eewWarning = JsonSerializer.Deserialize<Eew>(kevi.Json);
								if (eewWarning != null)
									EewController.UpdateWarning(eewWarning, time);
								break;
						}
						break;
				}
			}

			if (imageBytes != null || eewJson != null)
				KyoshinMonitorWatcher.LoadImageForReplay(time, imageBytes, eewJson);

			CurrentDisplayTime = time;
		};
		Runner.Finished += time =>
		{
			OnRealtimeDataUpdated((time, Array.Empty<RealtimeObservationPoint>(), Array.Empty<KyoshinEvent>()));
			WarningMessage = "リプレイファイルの再生が終了しました";
		};

		ReplayDescription = $"リプレイファイル {SpeedMultiplier:0.0}倍速";

		Eews = [];
		KyoshinEvents = [];
		ShakeDetectedRegions = [];
		MapNavigationRequest = null;
		EewController.Clear();
		OnEewUpdated(DateTime.Now, []);
		KyoshinMonitorWatcher.ResetHistories();
		EventStateTracker.Clear();
		_ = KyoshinMonitorWatcher.Initalize();

		// 観測点から地域マッピングを構築
		KyoshinMonitorWatcher.RealtimeDataUpdated += BuildRegionMap;

		// 時刻ジャンプ時のリセット
		KyoshinMonitorWatcher.TimeJumpDetected += OnTimeJumpDetected;

		Runner.Start();
	}

	private void OnTimeJumpDetected(TimeSpan jump)
	{
		EventStateTracker.Clear();
		KyoshinEvents = [];
		ShakeDetectedRegions = [];
	}

	private void BuildRegionMap((DateTime time, RealtimeObservationPoint[] data, KyoshinEvent[] events) e)
	{
		if (e.data == null || e.data.Length == 0)
			return;

		// 1回だけ実行
		KyoshinMonitorWatcher.RealtimeDataUpdated -= BuildRegionMap;

		// 全観測点から Region → SubRegion のマッピングを構築
		foreach (var point in e.data)
		{
			if (!RegionSubRegionMap.TryGetValue(point.Region, out var subRegions))
			{
				subRegions = [];
				RegionSubRegionMap[point.Region] = subRegions;
			}
			subRegions.Add(point.SubRegion);
		}
	}

	public async Task StopAsync()
	{
		if (Runner == null)
			return;
		var oldRunner = Runner;
		Runner = null;
		await oldRunner.StopAsync();
	}

	private void ProcessJmaXmlEew(string xml, DateTime time)
	{
		using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
		using var report = new JmaXmlDocument(stream);

		// サポート外であれば見なかったことにする
		if (report.Control.Title == "緊急地震速報配信テスト")
		{
			//Logger.LogInformation("dmdataから緊急地震速報のテスト電文を受信しました: {EventId} / {EditorialOffice}", report.Head.EventId, report.Control.EditorialOffice);
			return;
		}

		// 訓練･試験は今のところ非対応
		if (report.Control.Status != "通常")
			return;

		// 今のところ予報電文のみ対応
		if (report.Control.Title != "緊急地震速報（地震動予報）")
		{
			if (report.Control.Title == "緊急地震速報（警報）")
			{
				var earthquake2 = report.EarthquakeBody.Earthquake ?? throw new Exception("Earthquake 要素が見つかりません");
				var warningAreas2 = report.EarthquakeBody.Intensity?.Forecast?.Prefs.SelectMany(p => p.Areas.Where(a => a.Category?.Kind.Code == "19")).ToArray();
				EewController.UpdateWarning(new Models.Eew
				{
					Id = report.Head.EventId,
					Source = EewSource.Dmdata,
					DisplaySource = $"DM-D.S.S({report.Control.EditorialOffice}) 警報電文",
					ReceiveTime = time,
					SerialNo = int.Parse(report.Head.Serial),
					IsFinal = report.EarthquakeBody.NextAdvisory == "この情報をもって、緊急地震速報：最終報とします。",
					MaxIntensity = report.EarthquakeBody.Intensity?.Forecast?.ForecastIntFrom.ToJmaIntensity() ?? JmaIntensity.Unknown,
					IsIntensityOver = report.EarthquakeBody.Intensity?.Forecast?.ForecastIntTo == "over",
					Hypocenter = new EewHypocenter
					{
						OccurrenceTime = earthquake2.OriginTime?.DateTime ?? report.EarthquakeBody.Earthquake?.ArrivalTime?.DateTime ?? throw new Exception("OccurrenceTime が取得できません"),
						Place = earthquake2.Hypocenter.Area.Name,
						Location = CoordinateConverter.GetLocation(earthquake2.Hypocenter.Area.Coordinate.Value),
						Magnitude = earthquake2.Magnitude.TryGetFloatValue(out var m2) ? (float.IsNaN(m2) ? null : m2) : null,
						Depth = CoordinateConverter.GetDepth(earthquake2.Hypocenter.Area.Coordinate.Value) ?? -1,
						IsTemporary = earthquake2.Condition == "仮定震源要素",
					},

					IsWarning = true,
					WarningAreas = new EewWarningAreas
					{
						DisplaySource = "リプレイ 警報電文",
						SerialNo = int.Parse(report.Head.Serial),
						Codes = warningAreas2?.Select(a => a.Code).ToArray() ?? [],
						Names = EewAreaGroups.Compressor.Compress(warningAreas2?.Select(a => a.Name).ToArray() ?? []),
						IsWarningTelegram = true,
					},
				}, time);
				return;
			}
			//if (report.Control.Title != "緊急地震速報（予報）")
			//	Logger.LogWarning("dmdataからEEW予報以外の電文を受信しました: {Title}", report.Control.Title);
			return;
		}

		// 取消報
		if (report.Head.InfoType == "取消")
		{
			//Logger.LogInformation("dmdataからEEW取消報を受信しました: {EventId}", report.Head.EventId);
			EewController.Cancelled(report.Head.EventId, time);
			return;
		}
		//Logger.LogInformation("dmdataからEEWを受信しました: {EventId}", report.Head.EventId);

		var earthquake = report.EarthquakeBody.Earthquake ?? throw new Exception("Earthquake 要素が見つかりません");
		var warningAreas = report.EarthquakeBody.Intensity?.Forecast?.Prefs.SelectMany(p => p.Areas.Where(a => a.Category?.Kind.Code is "10" or "11" or "19")).ToArray();
		var eew = new Models.Eew
		{
			Id = report.Head.EventId,
			Source = EewSource.Dmdata,
			DisplaySource = $"リプレイ({report.Control.EditorialOffice})",
			ReceiveTime = time,
			SerialNo = int.Parse(report.Head.Serial),
			IsFinal = report.EarthquakeBody.NextAdvisory == "この情報をもって、緊急地震速報：最終報とします。",
			MaxIntensity = report.EarthquakeBody.Intensity?.Forecast?.ForecastIntFrom.ToJmaIntensity() ?? JmaIntensity.Unknown,
			IsIntensityOver = report.EarthquakeBody.Intensity?.Forecast?.ForecastIntTo == "over",
			// TODO LPGM
			Hypocenter = new EewHypocenter
			{
				OccurrenceTime = earthquake.OriginTime?.DateTime ?? report.EarthquakeBody.Earthquake?.ArrivalTime?.DateTime ?? throw new Exception("OccurrenceTime が取得できません"),
				Place = earthquake.Hypocenter.Area.Name,
				Location = CoordinateConverter.GetLocation(earthquake.Hypocenter.Area.Coordinate.Value),
				Magnitude = earthquake.Magnitude.TryGetFloatValue(out var m) ? (float.IsNaN(m) ? null : m) : null,
				Depth = CoordinateConverter.GetDepth(earthquake.Hypocenter.Area.Coordinate.Value) ?? -1,
				IsTemporary = earthquake.Condition == "仮定震源要素",
				Accuracy = new EewHypocenterAccuracy
				{
					IsLocked = earthquake.Hypocenter.Accuracy.EpicenterRank2 == 9,
					LocationAccuracy = earthquake.Hypocenter.Accuracy.EpicenterRank,
					DepthAccuracy = earthquake.Hypocenter.Accuracy.DepthRank,
					MagnitudeAccuracy = earthquake.Hypocenter.Accuracy.MagnitudeCalculationRank,
				},
			},
			IntensityForecastMap = report.EarthquakeBody.Intensity?.Forecast?.Prefs
				.SelectMany(p => p.Areas.Select(a => (a.Code, a.ForecastIntTo == "over" ? a.ForecastIntFrom.ToJmaIntensity() : a.ForecastIntTo.ToJmaIntensity())))
				.Where(a => a.Item2 != JmaIntensity.Unknown)
				.ToDictionary(k => k.Code, v => v.Item2),
			WarningAreas = (warningAreas?.Any() ?? false) ? new EewWarningAreas
			{
				DisplaySource = "リプレイ 予報電文",
				SerialNo = int.Parse(report.Head.Serial),
				Codes = warningAreas?.Select(a => a.Code).ToArray() ?? [],
				Names = EewAreaGroups.Compressor.Compress(warningAreas?.Select(a => a.Name).ToArray() ?? []),
			} : null,
			IsWarning = report.EarthquakeBody.Comments?.WarningCommentCode?.Contains("0201") ?? false,
		};

		EewController.Update(eew, time);
	}
}
