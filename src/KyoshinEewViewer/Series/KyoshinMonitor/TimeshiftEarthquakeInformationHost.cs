using KyoshinEewViewer.Core.Models;
using System;
using Splat;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.Series.KyoshinMonitor.Services;
using KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;
using System.Collections.Generic;
using System.Linq;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.CustomControl;
using SkiaSharp;
using System.Text;
using ZLinq;

namespace KyoshinEewViewer.Series.KyoshinMonitor;
public class TimeshiftEarthquakeInformationHost : EarthquakeInformationHost
{
	private EewController EewController { get; set; }
	private KyoshinMonitorWatchService KyoshinMonitorWatcher { get; }
	private TimerService TimerService { get; }

	private bool IsRunning { get; set; }
	private int TimeshiftSeconds { get; set; } = 0;

	private Dictionary<Guid, KyoshinEventLevel> KyoshinEventLevelCache { get; } = [];

	public override DateTime CurrentTime =>
		Config.Eew.SyncKyoshinMonitorPsWave ? KyoshinMonitorWatcher.CurrentDisplayTime : TimerService.CurrentTime.AddSeconds(-TimeshiftSeconds);

	public TimeshiftEarthquakeInformationHost(
		ILogManager logManager,
		KyoshinMonitorSeries series,
		KyoshinEewViewerConfiguration config,
		TimerService timerService,
		NotificationService notificationService,
		SoundPlayerService soundPlayer,
		WorkflowService workflowService
	) : base(true, config)
	{
		TimerService = timerService;
		EewController = new(logManager, series, config, notificationService, soundPlayer, workflowService) {
			IsReplay = true
		};
		EewController.EewUpdated += OnEewUpdated;
		KyoshinMonitorWatcher = new(logManager, Config, EewController);
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

		// TODO コピペになっているので微妙。なんとかしたい
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
			if (Config.KyoshinMonitor.UseExperimentalShakeDetect && e.events.Length != 0)
			{
				foreach (var evt in e.events)
				{
					// 現時刻で検知、もしくはレベル上昇していれば音声を再生
					// ただし Weaker は音を鳴らさない
					if (!KyoshinEventLevelCache.TryGetValue(evt.Id, out var lv) || lv < evt.Level)
						OnKyoshinEventUpdated((e.time, evt, KyoshinEventLevelCache.ContainsKey(evt.Id)));
					KyoshinEventLevelCache[evt.Id] = evt.Level;
				}
				// 存在しないイベントに対するキャッシュを削除
				foreach (var key in KyoshinEventLevelCache.Keys.ToArray())
					if (!e.events.Any(e => e.Id == key))
						KyoshinEventLevelCache.Remove(key);
			}

			UpateFocusPoint(e.time);
			OnRealtimeDataUpdated(e);
		};
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
		MapNavigationRequest = null;
		EewController.Clear();
		OnEewUpdated(DateTime.Now, []);
		KyoshinMonitorWatcher.ResetHistories();
		KyoshinEventLevelCache.Clear();
		KyoshinMonitorWatcher.Initalize();
		TimerService.StartMainTimer();
	}

	public void Stop()
	{
		IsRunning = false;
		// タイムシフト終了
		TimeshiftSeconds = 0;
		ReplayDescription = "";
	}
}
