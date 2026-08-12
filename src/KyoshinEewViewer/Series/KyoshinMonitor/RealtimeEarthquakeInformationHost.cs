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
using KyoshinEewViewer.Services.ExternalPublishers.Axis;
using KyoshinEewViewer.Services.ExternalPublishers.Axis.ApiModels;
using System.Text.Json;
using KyoshinEewViewer.Services.ExternalPublishers.Axis.ApiModels.Message;
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

	private KyoshinEventStateTracker EventStateTracker { get; } = new();

	public override DateTime CurrentTime =>
		Config.Eew.SyncKyoshinMonitorPsWave ? KyoshinMonitorWatcher.CurrentDisplayTime : TimerService.CurrentTime;

	public RealtimeEarthquakeInformationHost(
		ILogManager logManager,
		KyoshinEewViewerConfiguration config,
		EewController eewController,
		TimerService timerService,
		TelegramProvideService telegramProvider,
		AxisInformationProvider axisInformationProvider,
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

		// 全EEWソース受信失敗の判定
		this.WhenAnyValue(x => x.AxisReceiving, x => x.AxisDisconnected, x => x.IsSignalNowEewReceiving, x => x.DmdataReceiving, x => x.DmdataDisconnected, x => x.Config.Eew.EnableKyoshinMonitor, x => x.Config.KyoshinMonitor.ReceiveMode)
			.Subscribe(e => {
				AllEewSourceFailed = (!AxisReceiving || AxisDisconnected) &&
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
		_ = KyoshinMonitorWatcher.Initalize();

		// 観測点から地域マッピングを構築
		KyoshinMonitorWatcher.RealtimeDataUpdated += BuildRegionMap;

		// 時刻ジャンプ(スリープ復帰など)時のリセット
		KyoshinMonitorWatcher.TimeJumpDetected += OnTimeJumpDetected;

		TimerService.StartMainTimer();
		AxisInformationProvider.Initialize();
	}

	private void OnTimeJumpDetected(TimeSpan jump)
	{
		Logger.LogWarning($"時刻ジャンプによる強震モニタ履歴のリセットを実行します: {jump.TotalSeconds:F1}秒");
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

			// アプリ内の時刻は日本標準時の壁時計で統一しているため、マシンのタイムゾーンに依存しないよう明示的に変換する
			var originTime = eew.OriginDateTime.ToOffset(TimeSpan.FromHours(9)).DateTime;
			var reportTime = eew.ReportDateTime.ToOffset(TimeSpan.FromHours(9)).DateTime;

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
					OccurrenceTime = originTime,
					Place = eew.Hypocenter.Name,
					IsTemporary = depth == 10 && magnitude is { } m2 && Math.Abs(m2 - 1.0) < 0.01,
				},
				IsFinal = eew.Flag.IsFinal,
				ReceiveTime = reportTime,
				SerialNo = eew.Serial,
				MaxIntensity = eew.Intensity?.ToJmaIntensity() ?? JmaIntensity.Unknown,
				IsWarning = (eew.Text?.Contains("強い揺れ") ?? false) || eew.Intensity?.ToJmaIntensity() >= JmaIntensity.Int5Lower,
			}, reportTime);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "AXISのEEWメッセージ処理中にエラーが発生しました");
		}
	}
}
