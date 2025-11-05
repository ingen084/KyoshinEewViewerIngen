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
using KyoshinEewViewer.JmaXmlParser;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinMonitorLib;
using System.Text;
using System.Text.Json;

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

	public void ResetSpeedMultiplier()
	{
		SpeedMultiplier = 1;
	}

	private ReplayFileRunner? Runner { get; set; }

	private Dictionary<Guid, KyoshinEventLevel> KyoshinEventLevelCache { get; } = [];

	public override DateTime CurrentTime {
		get {
			var time = Runner?.CurrentTime ?? DateTime.Now;
			if (Config.Eew.SyncKyoshinMonitorPsWave)
				return time.AddSeconds(-1);
			return time;
		}
	}

	public ReplayFileEarthquakeInformationHost(
		ILogManager logManager,
		KyoshinMonitorSeries series,
		KyoshinEewViewerConfiguration config,
		NotificationService notificationService,
		SoundPlayerService soundPlayer,
		WorkflowService workflowService,
		ObservationPointsUpdateService observationPointsUpdateService
	) : base(true, config)
	{
		EewController = new(logManager, series, config, soundPlayer, workflowService) { IsReplay = true };
		EewController.EewUpdated += OnEewUpdated;
		KyoshinMonitorWatcher = new(logManager, Config, EewController, observationPointsUpdateService);
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
			if (e.data != null)
				WarningMessage = null;
			IsWorking = false;
			// CurrentDisplayTime = e.time;
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

	private void ProcessJmaXmlEew(string xml, DateTime time)
	{
		using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
		using var report = new JmaXmlDocument(stream);

		// サポート外であれば見なかったことにする
		if (report.Control.Title == "緊急地震速報配信テスト")
		{
			//Logger.LogInfo($"dmdataから緊急地震速報のテスト電文を受信しました: {report.Head.EventId} / {report.Control.EditorialOffice}");
			return;
		}

		// 訓練･試験は今のところ非対応
		if (report.Control.Status != "通常")
			return;

		// 今のところ予報電文のみ対応
		if (report.Control.Title != "緊急地震速報（地震動予報）")
		{
			//if (report.Control.Title != "緊急地震速報（予報）")
			//	Logger.LogWarning($"dmdataからEEW予報以外の電文を受信しました: {report.Control.Title}");
			return;
		}

		// 取消報
		if (report.Head.InfoType == "取消")
		{
			//Logger.LogInfo($"dmdataからEEW取消報を受信しました: {report.Head.EventId}");
			EewController.Cancelled(report.Head.EventId, time);
			return;
		}
		//Logger.LogInfo($"dmdataからEEWを受信しました: {report.Head.EventId}");

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
				Codes = warningAreas?.Select(a => a.Code).ToArray() ?? [],
				Names = EewAreaCompressor.Compress(warningAreas?.Select(a => a.Name).ToArray() ?? []),
			} : null,
			IsWarning = report.EarthquakeBody.Comments?.WarningCommentCode?.Contains("0201") ?? false,
		};

		EewController.Update(eew, time);
	}
}
