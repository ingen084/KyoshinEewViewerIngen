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
using KyoshinEewViewer.Services.ExtarnalPublishers.Axis;
using KyoshinEewViewer.Services.ExtarnalPublishers.Axis.ApiModels;
using System.Text.Json;
using KyoshinEewViewer.Services.ExtarnalPublishers.Axis.ApiModels.Message;
using KyoshinMonitorLib;
using KyoshinEewViewer.Core;

namespace KyoshinEewViewer.Series.KyoshinMonitor;

public class RealtimeEarthquakeInformationHost : EarthquakeInformationHost
{
	private ILogger Logger { get; }

	private bool IsRunning { get; set; }

	private EewController EewController { get; }
	public KyoshinMonitorWatchService KyoshinMonitorWatcher { get; }
	private SignalNowFileWatcher SignalNowEewReceiver { get; }
	public EewTelegramSubscriber EewTelegramSubscriber { get; }
	public AxisInformationProvider AxisInformationProvider { get; }
	private TimerService TimerService { get; }

	private Dictionary<Guid, KyoshinEventLevel> KyoshinEventLevelCache { get; } = [];

	public override DateTime CurrentTime =>
		Config.Eew.SyncKyoshinMonitorPsWave ? KyoshinMonitorWatcher.CurrentDisplayTime : TimerService.CurrentTime;

	public RealtimeEarthquakeInformationHost(
		ILogManager logManager,
		KyoshinEewViewerConfiguration config,
		EewController eewController,
		TimerService timerService,
		TelegramProvideService telegramProvider,
		AxisInformationProvider axisInformationProvider
	) : base(false, config)
	{
		ReplayDescription = "リアルタイム";

		Logger = logManager.GetLogger<RealtimeEarthquakeInformationHost>();
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

		AxisInformationProvider = axisInformationProvider;

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
		IsSignalNowEewReceiving = SignalNowEewReceiver.CanReceive;

		Config.Axis.WhenAnyValue(x => x.Enable).Subscribe(e => AxisReceiving = e);
		AxisInformationProvider.WhenAnyValue(x => x.IsConnected).Subscribe(e => {
			AxisDisconnected = !e || (!AxisInformationProvider.CurrentPayload?.Channels.Contains("eew") ?? true);
		});
		AxisInformationProvider.MessageReceived += AxisMessageReceived;
	}

	public void Start()
	{
		if (IsRunning)
			return;
		IsRunning = true;

		KyoshinEvents = [];
		KyoshinMonitorWatcher.ResetHistories();
		KyoshinEventLevelCache.Clear();
		KyoshinMonitorWatcher.Initalize();
		TimerService.StartMainTimer();
		AxisInformationProvider.Initialize();
	}

	public void Stop()
		=> IsRunning = false;

	private void AxisMessageReceived(AxisWebSocketMessage message)
	{
		try
		{
			if (message.Channel != "eew")
				return;

			Logger.LogDebug("AXIS のEEWを受信しました: " + JsonSerializer.Serialize(message));

			var eew = message.Message.Deserialize<EewMessage>();

			if ((eew?.Flag?.IsTraining ?? true) ||
				!(eew?.Hypocenter?.Depth?.Length > 2) ||
				!int.TryParse(eew.Hypocenter.Depth[..^2], out var depth))
				return;
			float? magnitude = float.TryParse(eew.Magnitude, out var m) ? m : null;

			if (eew.EventID == null)
				return;

			EewController.Update(new()
			{
				Id = eew.EventID,
				Source = Models.EewSource.Axis,
				DisplaySource = "AXIS",
				Hypocenter = new()
				{
					Depth = depth,
					Location = eew.Hypocenter.Coordinate?.Length >= 2 ? new(eew.Hypocenter.Coordinate[1], eew.Hypocenter.Coordinate[0]) : null,
					Magnitude = magnitude,
					OccurrenceTime = eew.OriginDateTime,
					Place = eew.Hypocenter.Name,
					IsTemporary = depth == 10 && magnitude is { } m2 && Math.Abs(m2 - 1.0) < 0.01,
				},
				IsFinal = eew.Flag.IsFinal,
				ReceiveTime = eew.ReportDateTime,
				SerialNo = eew.Serial,
				MaxIntensity = eew.Intensity?.ToJmaIntensity() ?? JmaIntensity.Unknown,
				IsWarning = (eew.Text?.Contains("強い揺れ") ?? false) || eew.Intensity?.ToJmaIntensity() >= JmaIntensity.Int5Lower,
			}, eew.ReportDateTime);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "AXISのEEWメッセージ処理中にエラーが発生しました");
		}
	}
}
