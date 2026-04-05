using KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;
using KyoshinEewViewer.Series.KyoshinMonitor.Services;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
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
using KyoshinEewViewer.Services.ExtarnalPublishers.P2pQuakeApi;
using KyoshinEewViewer.Services.ExtarnalPublishers.P2pQuakeApi.ApiModels;
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
	public P2pQuakeApiInformationProvider P2pQuakeApiInformationProvider { get; }
	private TimerService TimerService { get; }

	private KyoshinEventStateTracker EventStateTracker { get; } = new();
	private Dictionary<string, HashSet<string?>> RegionSubRegionMap { get; } = [];

	public override DateTime CurrentTime =>
		Config.Eew.SyncKyoshinMonitorPsWave ? KyoshinMonitorWatcher.CurrentDisplayTime : TimerService.CurrentTime;

	public RealtimeEarthquakeInformationHost(
		ILogManager logManager,
		KyoshinEewViewerConfiguration config,
		EewController eewController,
		TimerService timerService,
		TelegramProvideService telegramProvider,
		AxisInformationProvider axisInformationProvider,
		P2pQuakeApiInformationProvider p2pQuakeApiInformationProvider,
		ObservationPointsUpdateService observationPointsUpdateService
	) : base(false, config)
	{
		ReplayDescription = "リアルタイム";

		Logger = logManager.GetLogger<RealtimeEarthquakeInformationHost>();
		TimerService = timerService;
		EewController = eewController;
		EewController.EewUpdated += OnEewUpdated;
		TimerService.TimerElapsed += t => EewController.TimerElapsed(t);
		KyoshinMonitorWatcher = new KyoshinMonitorWatchService(logManager, Config, EewController, observationPointsUpdateService);
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
		P2pQuakeApiInformationProvider = p2pQuakeApiInformationProvider;

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
		IsSignalNowEewReceiving = SignalNowEewReceiver.CanReceive;

		Config.Axis.WhenAnyValue(x => x.Enable).Subscribe(e => AxisReceiving = e);
		AxisInformationProvider.WhenAnyValue(x => x.IsConnected).Subscribe(e => {
			AxisDisconnected = !e || (!AxisInformationProvider.CurrentPayload?.Channels.Contains("eew") ?? true);
		});
		AxisInformationProvider.MessageReceived += AxisMessageReceived;

		Config.P2pQuakeApi.WhenAnyValue(x => x.Enable).Subscribe(e => P2pQuakeApiReceiving = e);
		P2pQuakeApiInformationProvider.WhenAnyValue(x => x.IsConnected).Subscribe(e => P2pQuakeApiDisconnected = !e);
		P2pQuakeApiInformationProvider.MessageReceived += P2pQuakeApiMessageReceived;

		// 全EEWソース受信失敗の判定
		this.WhenAnyValue(x => x.AxisReceiving, x => x.AxisDisconnected, x => x.P2pQuakeApiReceiving, x => x.P2pQuakeApiDisconnected, x => x.IsSignalNowEewReceiving, x => x.DmdataReceiving, x => x.DmdataDisconnected)
			.Subscribe(e => {
				AllEewSourceFailed = (!AxisReceiving || AxisDisconnected) &&
									 (!P2pQuakeApiReceiving || P2pQuakeApiDisconnected) &&
									 !IsSignalNowEewReceiving &&
									 (!DmdataReceiving || DmdataDisconnected) &&
									 (!Config.Eew.EnableKyoshinMonitor || Config.KyoshinMonitor.ReceiveMode == KyoshinEewViewerConfiguration.KyoshinMonitorConfig.Mode.None);
			});
	}

	public void Start()
	{
		if (IsRunning)
			return;
		IsRunning = true;

		KyoshinEvents = [];
		ShakeDetectedRegions = [];
		KyoshinMonitorWatcher.ResetHistories();
		EventStateTracker.Clear();
		KyoshinMonitorWatcher.Initalize().ConfigureAwait(false);

		// 観測点から地域マッピングを構築
		KyoshinMonitorWatcher.RealtimeDataUpdated += BuildRegionMap;

		TimerService.StartMainTimer();
		AxisInformationProvider.Initialize();
		P2pQuakeApiInformationProvider.Initialize();
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

		Logger.LogDebug($"地域マッピングを構築しました: {RegionSubRegionMap.Count} 地域");
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

	private void P2pQuakeApiMessageReceived(P2pQuakeApiBaseMessage message)
	{
		try
		{
			if (message is not P2pQuakeApiEewMessage eew)
				return;

			if (eew.Test)
				return;

			Logger.LogDebug("P2P地震情報 APIのEEW警報を受信しました");

			var now = DateTime.Now;

			// キャンセル判定
			if (eew.Cancelled)
			{
				if (eew.Issue?.EventId != null)
					EewController.WarningCancelled(eew.Issue.EventId, now);
				return;
			}

			if (eew.Issue?.EventId == null || eew.Earthquake?.Hypocenter == null)
				return;

			if (!int.TryParse(eew.Issue.Serial, out var serial))
				return;

			var hypo = eew.Earthquake.Hypocenter;
			var hasLocation = hypo.Latitude > -200 && hypo.Longitude > -200;
			var depth = (int)hypo.Depth;
			if (depth < 0) depth = 0;
			float? magnitude = hypo.Magnitude >= 0 ? (float)hypo.Magnitude : null;

			if (!DateTime.TryParse(eew.Earthquake.OriginTime, out var originTime))
				return;

			// 予測震度エリアから最大震度を取得
			var maxIntensity = JmaIntensity.Unknown;
			var isIntensityOver = false;
			// 警報地域を抽出
			var warningAreaNames = new List<string>();
			var warningAreaCodes = new List<int>();
			if (eew.Areas is { Length: > 0 })
			{
				foreach (var area in eew.Areas)
				{
					var fromIntensity = P2pQuakeApiScaleConverter.ToJmaIntensity(area.ScaleFrom);
					var toIntensity = P2pQuakeApiScaleConverter.IsOver(area.ScaleTo)
						? fromIntensity
						: P2pQuakeApiScaleConverter.ToJmaIntensity(area.ScaleTo);
					var areaMax = fromIntensity > toIntensity ? fromIntensity : toIntensity;
					if (areaMax > maxIntensity)
					{
						maxIntensity = areaMax;
						isIntensityOver = P2pQuakeApiScaleConverter.IsOver(area.ScaleFrom) || P2pQuakeApiScaleConverter.IsOver(area.ScaleTo);
					}

					if (area.KindCode is "10" or "11" or "19" && area.Name != null)
					{
						warningAreaNames.Add(area.Name);
						warningAreaCodes.Add(0);
					}
				}
			}

			EewController.UpdateWarning(new()
			{
				Id = eew.Issue.EventId,
				Source = EewSource.P2pQuakeApi,
				DisplaySource = "P2P地震情報 JSON API",
				Hypocenter = new()
				{
					Depth = depth,
					Location = hasLocation ? new((float)hypo.Latitude, (float)hypo.Longitude) : null,
					Magnitude = magnitude,
					OccurrenceTime = originTime,
					Place = hypo.Name,
					IsTemporary = eew.Earthquake.Condition == "仮定震源要素",
				},
				IsFinal = false,
				ReceiveTime = now,
				SerialNo = serial,
				MaxIntensity = maxIntensity,
				IsIntensityOver = isIntensityOver,
				IsWarning = true,
				WarningAreas = new EewWarningAreas
				{
					DisplaySource = "P2P地震情報 JSON API",
					SerialNo = serial,
					Codes = warningAreaCodes.ToArray(),
					Names = warningAreaNames.ToArray(),
					IsWarningTelegram = true,
				},
			}, now);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "P2P地震情報 APIのEEWメッセージ処理中にエラーが発生しました");
		}
	}
}
