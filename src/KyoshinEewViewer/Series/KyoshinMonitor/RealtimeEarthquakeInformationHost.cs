using KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;
using KyoshinEewViewer.Series.KyoshinMonitor.Services;
using KyoshinEewViewer.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using KyoshinEewViewer.Core.Models;
using Splat;
using KyoshinEewViewer.CustomControl;
using SkiaSharp;
using KyoshinEewViewer.Map;
using ReactiveUI;

namespace KyoshinEewViewer.Series.KyoshinMonitor;

public class RealtimeEarthquakeInformationHost : EarthquakeInformationHost
{
	private bool IsRunning { get; set; }

	private EewController EewController { get; }
	public KyoshinMonitorWatchService KyoshinMonitorWatcher { get; }
	private SignalNowFileWatcher SignalNowEewReceiver { get; }
	public EewTelegramSubscriber EewTelegramSubscriber { get; }
	private TimerService TimerService { get; }

	private Dictionary<Guid, KyoshinEventLevel> KyoshinEventLevelCache { get; } = [];

	public override DateTime CurrentTime =>
		Config.Eew.SyncKyoshinMonitorPsWave ? KyoshinMonitorWatcher.CurrentDisplayTime : TimerService.CurrentTime;

	public RealtimeEarthquakeInformationHost(
		ILogManager logManager,
		KyoshinEewViewerConfiguration config,
		EewController eewController,
		TimerService timerService,
		TelegramProvideService telegramProvider
	) : base(false, config)
	{
		SplatRegistrations.RegisterLazySingleton<RealtimeEarthquakeInformationHost>();

		ReplayDescription = "リアルタイム";

		TimerService = timerService;
		EewController = eewController;
		EewController.EewUpdated += OnEewUpdated;
		TimerService.TimerElapsed += t => EewController.TimerElapsed(t);
		KyoshinMonitorWatcher = new KyoshinMonitorWatchService(logManager, Config, EewController);
		KyoshinMonitorWatcher.RealtimeDataUpdated += OnRealtimeDataUpdated;
		TimerService.DelayedTimerElapsed += t =>
		{
			if (!IsRunning)
				return;
			KyoshinMonitorWatcher.TimerElapsed(t).Wait();
		};
		SignalNowEewReceiver = new SignalNowFileWatcher(logManager, config, EewController, TimerService);
		EewTelegramSubscriber = new EewTelegramSubscriber(logManager, EewController, telegramProvider, TimerService);

		EewTelegramSubscriber.WhenAnyValue(x => x.Enabled).Subscribe(x => DmdataReceiving = x);
		EewTelegramSubscriber.WhenAnyValue(x => x.WarningOnlyEnabled).Subscribe(x => DmdataWarningOnlyReceiving = x);
		EewTelegramSubscriber.WhenAnyValue(x => x.IsDisconnected).Subscribe(x => DmdataDisconnected = x);
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
			RealtimePoints = e.data?.OrderByDescending(p => p.LatestIntensity ?? -1000, null);

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
		IsSignalNowEewReceiving = SignalNowEewReceiver.CanReceive;
	}

	public void Start()
	{
		if (IsRunning)
			return;
		KyoshinEvents = [];
		KyoshinMonitorWatcher.ResetHistories();
		KyoshinEventLevelCache.Clear();
		KyoshinMonitorWatcher.Initalize();
		TimerService.StartMainTimer();
		IsRunning = true;
	}

	public void Stop()
		=> IsRunning = false;
}
