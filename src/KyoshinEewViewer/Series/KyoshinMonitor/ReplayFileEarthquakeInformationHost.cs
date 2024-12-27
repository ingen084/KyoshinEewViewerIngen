using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.EarthquakeReplay;
using KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;
using KyoshinEewViewer.Series.KyoshinMonitor.Services;
using KyoshinEewViewer.Services;
using ReactiveUI;
using System;
using System.Collections.Generic;
using Splat;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.CustomControl;
using SkiaSharp;

namespace KyoshinEewViewer.Series.KyoshinMonitor;

public class ReplayFileEarthquakeInformationHost : EarthquakeInformationHost
{
	private EewController EewController { get; set; }
	private KyoshinMonitorWatchService KyoshinMonitorWatcher { get; }

	public bool IsRunning => Runner?.IsPlaying ?? false;

	private ReplayFileHeader? _currentHeader;
	public ReplayFileHeader? CurrentHeader
	{
		get => _currentHeader;
		set => this.RaiseAndSetIfChanged(ref _currentHeader, value);
	}

	private ReplayData[]? _currentData;
	public ReplayData[]? CurrentData
	{
		get => _currentData;
		set => this.RaiseAndSetIfChanged(ref _currentData, value);
	}

	private ReplayFileHeader? _loadedHeader;
	public ReplayFileHeader? LoadedHeader
	{
		get => _loadedHeader;
		set => this.RaiseAndSetIfChanged(ref _loadedHeader, value);
	}

	private ReplayData[]? _loadedData;
	public ReplayData[]? LoadedData
	{
		get => _loadedData;
		set => this.RaiseAndSetIfChanged(ref _loadedData, value);
	}

	private float _speedMultiplier = 1;
	public float SpeedMultiplier
	{
		get => _speedMultiplier;
		set {
			this.RaiseAndSetIfChanged(ref _speedMultiplier, value);
			if (Runner != null)
				Runner.SpeedMultiplier = value;
			ReplayDescription = $"リプレイファイル {SpeedMultiplier:0.0}倍速";
		}
	}

	private ReplayFileRunner? Runner { get; set; }

	private Dictionary<Guid, KyoshinEventLevel> KyoshinEventLevelCache { get; } = [];

	public override DateTime CurrentTime => Runner?.CurrentTime ?? DateTime.MaxValue;

	public ReplayFileEarthquakeInformationHost(
		ILogManager logManager,
		KyoshinEewViewerConfiguration config,
		NotificationService notificationService,
		SoundPlayerService soundPlayer,
		WorkflowService workflowService
	) : base(true, config)
	{
		SplatRegistrations.RegisterLazySingleton<ReplayFileEarthquakeInformationHost>();

		EewController = new(logManager, config, notificationService, soundPlayer, workflowService) { IsReplay = true };
		EewController.EewUpdated += OnEewUpdated;
		KyoshinMonitorWatcher = new(logManager, Config, EewController);
		KyoshinMonitorWatcher.RealtimeDataUpdated += OnRealtimeDataUpdated;
		KyoshinMonitorWatcher.WarningMessageUpdated += m => WarningMessage = m;
		KyoshinMonitorWatcher.RealtimeDataParseProcessStarted += t => IsWorking = true;

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
						//case JmaXmlTelegramReplayData jma:
						//	EewController.JmaTelegramUpdated(jma);
						//	break;
				}
			}

			if (imageBytes != null || eewJson != null)
				KyoshinMonitorWatcher.LoadImageForReplay(time, imageBytes, eewJson);
		};
		Runner.Finished += time =>
		{
			OnRealtimeDataUpdated((time, Array.Empty<RealtimeObservationPoint>(), Array.Empty<KyoshinEvent>()));
			WarningMessage = "リプレイファイルの再生が終了しました";
		};

		ReplayDescription = $"リプレイファイル {SpeedMultiplier:0.0}倍速";

		Eews = [];
		KyoshinEvents = [];
		MapNavigationRequest = null;
		EewController.Clear();
		OnEewUpdated(DateTime.Now, []);
		KyoshinMonitorWatcher.ResetHistories();
		KyoshinEventLevelCache.Clear();
		KyoshinMonitorWatcher.Initalize();

		Runner.Start();
	}

	public async Task StopAsync()
	{
		if (Runner == null)
			return;
		var oldRunner = Runner;
		Runner = null;
		await oldRunner.StopAsync();
	}
}
