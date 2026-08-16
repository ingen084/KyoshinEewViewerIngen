using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Services;
using KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;
using KyoshinEewViewer.Services;
using R3;
using SkiaSharp;
using Splat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KyoshinEewViewer.Series.KyoshinMonitor;
public class TimeshiftEarthquakeInformationHost : EarthquakeInformationHost
{
	private EewController EewController { get; set; }
	public EewPointForecastController PointForecastController { get; }
	private KyoshinMonitorWatchService KyoshinMonitorWatcher { get; }
	private TimerService TimerService { get; }

	private bool IsRunning { get; set; }
	private int TimeshiftSeconds { get; set; } = 0;

	private KyoshinEventStateTracker EventStateTracker { get; } = new();

	public override DateTime CurrentTime =>
		Config.Eew.SyncKyoshinMonitorPsWave ? KyoshinMonitorWatcher.CurrentDisplayTime : TimerService.CurrentTime.AddSeconds(-TimeshiftSeconds);

	public TimeshiftEarthquakeInformationHost(
		ILogManager logManager,
		KyoshinMonitorSeries series,
		KyoshinEewViewerConfiguration config,
		TimerService timerService,
		NotificationService notificationService,
		SoundPlayerService soundPlayer,
		WorkflowService workflowService,
		ObservationPointsUpdateService observationPointsUpdateService
	) : base(true, config)
	{
		TimerService = timerService;
		EewController = new(logManager, series, config, soundPlayer, workflowService) {
			IsReplay = true
		};
		PointForecastController = new(logManager, config, EewController, timerService, () => CurrentTime);
		EewController.EewUpdated += OnEewUpdated;
		KyoshinMonitorWatcher = new(logManager, Config, EewController, observationPointsUpdateService);
		KyoshinMonitorWatcher.RealtimeDataUpdated += OnRealtimeDataUpdated;
		KyoshinMonitorWatcher.WarningMessageUpdated += m => WarningMessage = m;
		KyoshinMonitorWatcher.RealtimeDataParseProcessStarted += t => IsWorking = true;

		TimerService.DelayedTimerElapsed += (time) =>
		{
			if (!IsRunning)
				return;

			var shiftedTime = time.AddSeconds(-TimeshiftSeconds);
			KyoshinMonitorWatcher.TimerElapsed(shiftedTime).Wait();
		};
		TimerService.TimerElapsed += (time) =>
		{
			if (!IsRunning)
				return;

			var shiftedTime = time.AddSeconds(-TimeshiftSeconds);
			EewController.TimerElapsed(shiftedTime);
		};

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
			CurrentDisplayTime = e.time;
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

		// 全EEWソース受信失敗の判定
		Observable.CombineLatest(
				this.ObservePropertyChanged(x => x.Config, x => x.Eew, x => x.EnableKyoshinMonitor).AsUnitObservable(),
				this.ObservePropertyChanged(x => x.Config, x => x.KyoshinMonitor, x => x.ReceiveMode).AsUnitObservable())
			.Subscribe(e => {
				AllEewSourceFailed = !Config.Eew.EnableKyoshinMonitor || Config.KyoshinMonitor.ReceiveMode == KyoshinEewViewerConfiguration.KyoshinMonitorConfig.Mode.None;
			});
	}

	public void Start(int timeshiftSeconds)
	{
		// タイムシフト開始
		TimeshiftSeconds = timeshiftSeconds;

		var sb = new StringBuilder("タイムシフト ");
		var time = TimeSpan.FromSeconds(TimeshiftSeconds);
		if (time.TotalHours >= 1)
			sb.Append((int)time.TotalHours + "時間");
		if (time.Minutes > 0)
			sb.Append(time.Minutes + "分");
		if (time.Seconds > 0)
			sb.Append(time.Seconds + "秒");
		sb.Append('前');

		ReplayDescription = sb.ToString();
		IsRunning = true;

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

		// 時刻ジャンプ(スリープ復帰など)時のリセット
		KyoshinMonitorWatcher.TimeJumpDetected += OnTimeJumpDetected;

		TimerService.StartMainTimer();
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

	public void Stop()
	{
		IsRunning = false;
		// タイムシフト終了
		TimeshiftSeconds = 0;
		ReplayDescription = "";
	}
}
