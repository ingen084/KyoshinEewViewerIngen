using Avalonia;
using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Series.KyoshinMonitor.Events;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Services;
using KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;
using KyoshinEewViewer.Services;
using KyoshinMonitorLib;
using ReactiveUI;
using SkiaSharp;
using Splat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Location = KyoshinMonitorLib.Location;

namespace KyoshinEewViewer.Series.KyoshinMonitor;

public class KyoshinMonitorSeries : SeriesBase
{
	public SoundCategory SoundCategory { get; } = new("KyoshinMonitor", "強震モニタ");
	private Sound WeakShakeDetectedSound { get; }
	private Sound MediumShakeDetectedSound { get; }
	private Sound StrongShakeDetectedSound { get; }
	private Sound StrongerShakeDetectedSound { get; }

	public KyoshinMonitorSeries() : this(null, null)
	{ }
	public KyoshinMonitorSeries(NotificationService? notificationService, TelegramProvideService? telegramProvideService) : base("強震モニタ", new FontIcon { Glyph = "\xe3b1", FontFamily = new("IconFont") })
	{
		KyoshinMonitorLayer = new(this);
		NotificationService = notificationService ?? Locator.Current.GetService<NotificationService>();
		EewController = new(SoundCategory, NotificationService);
		KyoshinMonitorWatcher = new(EewController);
		SignalNowEewReceiver = new(EewController, this);
		EewTelegramSubscriber = new(EewController, telegramProvideService ?? Locator.Current.GetService<TelegramProvideService>() ?? throw new Exception("TelegramProvideService の解決に失敗しました"));
		MapPadding = new Thickness(0, 0, 300, 0);

		WeakShakeDetectedSound = SoundPlayerService.RegisterSound(SoundCategory, "WeakShakeDetected", "揺れ検出(震度1未満)", "鳴動させるためには揺れ検出の設定を有効にしている必要があります。");
		MediumShakeDetectedSound = SoundPlayerService.RegisterSound(SoundCategory, "MediumShakeDetected", "揺れ検出(震度1以上3未満)", "震度上昇時にも鳴動します。\n鳴動させるためには揺れ検出の設定を有効にしている必要があります。");
		StrongShakeDetectedSound = SoundPlayerService.RegisterSound(SoundCategory, "StrongShakeDetected", "揺れ検出(震度3以上5弱未満)", "震度上昇時にも鳴動します。\n鳴動させるためには揺れ検出の設定を有効にしている必要があります。");
		StrongerShakeDetectedSound = SoundPlayerService.RegisterSound(SoundCategory, "StrongerShakeDetected", "揺れ検出(震度5弱以上)", "震度上昇時にも鳴動します。\n鳴動させるためには揺れ検出の設定を有効にしている必要があります。");

		#region dev用モック
		if (Design.IsDesignMode)
		{
#if DEBUG
			CurrentTime = DateTime.Now;
			IsDebug = true;

			IsWorking = true;
			IsSignalNowEewReceiving = true;
			IsLast10SecondsEewReceiving = false;

			ShowEewAccuracy = true;

			WarningMessage = "これは けいこくめっせーじ じゃ！";

			var points = new List<RealtimeObservationPoint>()
			{
				new RealtimeObservationPoint(new ObservationPoint{ Region = "テスト", Name = "テスト", Point = new() }) { LatestIntensity = 0.1, LatestColor = new SKColor(255, 0, 0, 255) },
				new RealtimeObservationPoint(new ObservationPoint{ Region = "テスト", Name = "テスト", Point = new() }) { LatestIntensity = 0.2, LatestColor = new SKColor(0, 255, 0, 255) },
				new RealtimeObservationPoint(new ObservationPoint{ Region = "テスト", Name = "テスト", Point = new() }) { LatestIntensity = 0.3, LatestColor = new SKColor(255, 0, 255, 255) },
				new RealtimeObservationPoint(new ObservationPoint{ Region = "テスト", Name = "テスト", Point = new() }) { LatestIntensity = 0.4, LatestColor = new SKColor(255, 255, 0, 255) },
				new RealtimeObservationPoint(new ObservationPoint{ Region = "テスト", Name = "テスト", Point = new() }) { LatestIntensity = 0.6, LatestColor = new SKColor(0, 255, 255, 255) },
				new RealtimeObservationPoint(new ObservationPoint{ Region = "テスト", Name = "テスト", Point = new() }) { LatestIntensity = 0.7, LatestColor = new SKColor(255, 255, 255, 255) },
				new RealtimeObservationPoint(new ObservationPoint{ Region = "テスト", Name = "テスト", Point = new() }) { LatestIntensity = 0.8, LatestColor = new SKColor(0, 0, 0, 255) },
				new RealtimeObservationPoint(new ObservationPoint{ Region = "テスト", Name = "テスト", Point = new() }) { LatestIntensity = 1.0, LatestColor = new SKColor(255, 0, 0, 255) },
			};

			RealtimePoints = points.OrderByDescending(p => p.LatestIntensity ?? -1, null);
			KyoshinEvents = new KyoshinEvent[]
			{
				new(DateTime.Now, new RealtimeObservationPoint(new ObservationPoint{ Region = "テスト", Name = "テスト", Point = new(), Location = new() }) { LatestIntensity = 0.1, LatestColor = new SKColor(255, 0, 0, 255) }),
				new(DateTime.Now, new RealtimeObservationPoint(new ObservationPoint{ Region = "テスト2", Name = "テスト2", Point = new(), Location = new() }) { LatestIntensity = 5.1, LatestColor = new SKColor(255, 0, 0, 255) }),
			};

			Eews = new[]
			{
				new EewMock
				{
					Id = "a",
					Count = 999,
					SourceDisplay = "大阪管区気象台",
					Intensity = JmaIntensity.Int3,
					IsWarning = false,
					ReceiveTime = DateTime.Now,
					OccurrenceTime = DateTime.Now,
					Magnitude = 9.9f,
					Depth = 999,
					DepthAccuracy = 1,
					LocationAccuracy = 1,
					MagnitudeAccuracy = 2,
					Place = "通常テスト",
					IsLocked = true,
				},
				new EewMock
				{
					Id = "b",
					Count = 2,
					SourceDisplay = "あいうえお",
					Intensity = JmaIntensity.Int5Lower,
					IsWarning = true,
					ReceiveTime = DateTime.Now,
					OccurrenceTime = DateTime.Now,
					Magnitude = 1.0f,
					Depth = 10,
					DepthAccuracy = 1,
					LocationAccuracy = 1,
					MagnitudeAccuracy = 8,
					IsTemporaryEpicenter = true,
					Place = "仮定震源要素",
					WarningAreaNames = new[] {
						"岩手県沿岸南部",
						"岩手県内陸南部",
						"宮城県北部",
						"岩手県沿岸北部",
						"宮城県南部",
						"福島県浜通り",
						"福島県中通り",
						"岩手県内陸北部",
						"山形県村山",
					},
				},
				//new EewMock
				//{
				//	Id = "a",
				//	SourceDisplay = "強震モニタ",
				//	Intensity = JmaIntensity.Unknown,
				//	IsWarning = false,
				//	IsCancelled = true,
				//	ReceiveTime = DateTime.Now,
				//	OccurrenceTime = DateTime.Now,
				//	Depth = 0,
				//	Place = "キャンセル",
				//},
			};

			IsReplay = true;
#endif
			return;
		}
		#endregion


		OverlayLayers = new[] { KyoshinMonitorLayer };

		MessageBus.Current.Listen<DisplayWarningMessageUpdated>().Subscribe(m => WarningMessage = m.Message);
		WorkingTime = DateTime.Now;

		MessageBus.Current.Listen<KyoshinMonitorReplayRequested>().Subscribe(m =>
		{
			KyoshinMonitorWatcher.OverrideSource = m.BasePath;
			KyoshinMonitorWatcher.OverrideDateTime = m.Time;
		});

		KyoshinMonitorWatcher.RealtimeDataParseProcessStarted += t =>
		{
			IsWorking = true;
			WorkingTime = t;
		};

		// EEW受信
		EewController.EewUpdated += e =>
		{
			var eews = e.eews.Where(e => !e.IsCancelled && e.UpdatedTime - WorkingTime < TimeSpan.FromMilliseconds(ConfigurationService.Current.Timer.Offset * 2));
			KyoshinMonitorLayer.CurrentEews = Eews = eews.ToArray();

			// 塗りつぶし地域組み立て
			var intensityAreas = eews.SelectMany(e => e.ForecastIntensityMap ?? new()).GroupBy(p => p.Key, p => p.Value).ToDictionary(p => p.Key, p => p.Max());
			var warningAreaCodes = eews.SelectMany(e => e.WarningAreaCodes ?? Array.Empty<int>()).Distinct().ToArray();
			if (ConfigurationService.Current.Eew.FillForecastIntensity && intensityAreas.Any())
				CustomColorMap = new()
				{
					{
						LandLayerType.EarthquakeInformationSubdivisionArea,
						intensityAreas.ToDictionary(p => p.Key, p => FixedObjectRenderer.IntensityPaintCache[p.Value].b.Color)
					},
				};
			else if (ConfigurationService.Current.Eew.FillWarningArea && warningAreaCodes.Any())
				CustomColorMap = new()
				{
					{
						LandLayerType.EarthquakeInformationSubdivisionArea,
						warningAreaCodes.ToDictionary(c => c, c => SKColors.Tomato)
					},
				};
			else
				CustomColorMap = null;

			UpateFocusPoint(e.time);
		};
		KyoshinMonitorWatcher.RealtimeDataUpdated += e =>
		{
			RealtimePoints = e.data?.OrderByDescending(p => p.LatestIntensity ?? -1000, null);

			if (e.data != null)
				WarningMessage = null;
			IsWorking = false;
			CurrentTime = e.time;
			KyoshinMonitorLayer.ObservationPoints = e.data;

			KyoshinMonitorLayer.KyoshinEvents = KyoshinEvents = e.events;
			if (ConfigurationService.Current.KyoshinMonitor.UseExperimentalShakeDetect && e.events.Any())
			{
				foreach (var evt in e.events)
				{
					// 現時刻で検知、もしくはレベル上昇していれば音声を再生
					// ただし Weaker は音を鳴らさない
					if (!KyoshinEventLevelCache.TryGetValue(evt.Id, out var lv) || lv < evt.Level)
					{
						EventHookService.Run("KMONI_SHAKE_DETECTED", new()
						{
							{ "SHAKE_DETECT_ID", evt.Id.ToString() },
							{ "SHAKE_DETECT_LEVEL", evt.Level.ToString() },
							{ "SHAKE_DETECT_MAX_INTENSITY", evt.Points.Max(p => p.LatestIntensity)?.ToString("0.0") ?? "null" },
							{ "SHAKE_DETECT_REGIONS", string.Join(',', evt.Points.Select(p => p.Region.Length > 3 ? p.Region[..3] : p.Region).Distinct()) },
						}).ConfigureAwait(false);

						switch (evt.Level)
						{
							case KyoshinEventLevel.Weak:
								WeakShakeDetectedSound.Play();
								break;
							case KyoshinEventLevel.Medium:
								MediumShakeDetectedSound.Play();
								break;
							case KyoshinEventLevel.Strong:
								StrongShakeDetectedSound.Play();
								break;
							case KyoshinEventLevel.Stronger:
								StrongerShakeDetectedSound.Play();
								break;
						}
						MessageBus.Current.SendMessage(new KyoshinShakeDetected(evt, KyoshinEventLevelCache.ContainsKey(evt.Id)));

					}
					KyoshinEventLevelCache[evt.Id] = evt.Level;
				}
				// 存在しないイベントに対するキャッシュを削除
				foreach (var key in KyoshinEventLevelCache.Keys.ToArray())
					if (!e.events.Any(e => e.Id == key))
						KyoshinEventLevelCache.Remove(key);
			}

			UpateFocusPoint(e.time);
		};

		IsSignalNowEewReceiving = SignalNowEewReceiver.CanReceive;

		ConfigurationService.Current.Timer.WhenAnyValue(x => x.TimeshiftSeconds).Subscribe(x => IsReplay = x < 0);
		ConfigurationService.Current.KyoshinMonitor.WhenAnyValue(x => x.ListRenderMode)
			.Subscribe(x => ListRenderMode = Enum.TryParse<RealtimeDataRenderMode>(ConfigurationService.Current.KyoshinMonitor.ListRenderMode, out var mode) ? mode : ListRenderMode);
		ListRenderMode = Enum.TryParse<RealtimeDataRenderMode>(ConfigurationService.Current.KyoshinMonitor.ListRenderMode, out var mode) ? mode : ListRenderMode;

		ConfigurationService.Current.Eew.WhenAnyValue(x => x.ShowDetails).Subscribe(x => ShowEewAccuracy = x);

		Task.Run(() => KyoshinMonitorWatcher.Start());
	}

	private EewController EewController { get; }
	private NotificationService? NotificationService { get; }
	public KyoshinMonitorWatchService KyoshinMonitorWatcher { get; }
	private SignalNowFileWatcher SignalNowEewReceiver { get; }
	public EewTelegramSubscriber EewTelegramSubscriber { get; }

	private KyoshinMonitorLayer KyoshinMonitorLayer { get; }

	private KyoshinMonitorView? control;
	public override Control DisplayControl => control ?? throw new InvalidOperationException("初期化前にコントロールが呼ばれています");

	private Dictionary<Guid, KyoshinEventLevel> KyoshinEventLevelCache { get; } = new();

	public override void Activating()
	{
		if (control != null)
			return;

		control = new KyoshinMonitorView
		{
			DataContext = this
		};
	}

	private void UpateFocusPoint(DateTime time)
	{
		// 震度が不明でない、キャンセルされてない、最終報から1分未満、座標が設定されている場合のみズーム
		var targetEews = Eews.Where(e => /*(e.Source == EewSource.SignalNowProfessional && e.Intensity != JmaIntensity.Unknown) &&*/ !e.IsCancelled && (!e.IsFinal || (time - e.ReceiveTime).Minutes < 1) && e.Location != null);
		if (!targetEews.Any() && (!ConfigurationService.Current.KyoshinMonitor.UseExperimentalShakeDetect || !KyoshinEvents.Any(k => k.Level > KyoshinEventLevel.Weaker)))
		{
			OnMapNavigationRequested(null);
			return;
		}

		// 自動ズーム範囲を計算
		var minLat = float.MaxValue;
		var maxLat = float.MinValue;
		var minLng = float.MaxValue;
		var maxLng = float.MinValue;
		void CheckLocation(Location p)
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

		// 必須範囲
		var minLat2 = float.MaxValue;
		var maxLat2 = float.MinValue;
		var minLng2 = float.MaxValue;
		var maxLng2 = float.MinValue;
		void CheckLocation2(Location p)
		{
			if (minLat2 > p.Latitude)
				minLat2 = p.Latitude;
			if (minLng2 > p.Longitude)
				minLng2 = p.Longitude;

			if (maxLat2 < p.Latitude)
				maxLat2 = p.Latitude;
			if (maxLng2 < p.Longitude)
				maxLng2 = p.Longitude;
		}

		// EEW
		foreach (var l in targetEews.Select(e => e.Location))
		{
			CheckLocation2(l!);
			CheckLocation(new(l!.Latitude - 1, l.Longitude - 1));
			CheckLocation(new(l.Latitude + 1, l.Longitude + 1));
		}
		// Event
		foreach (var e in KyoshinEvents.Where(k => k.Level > KyoshinEventLevel.Weaker))
		{
			CheckLocation2(e.TopLeft);
			CheckLocation2(e.BottomRight);
			CheckLocation(new(e.TopLeft.Latitude - .5f, e.TopLeft.Longitude - .5f));
			CheckLocation(new(e.BottomRight.Latitude + .5f, e.BottomRight.Longitude + .5f));
		}

		OnMapNavigationRequested(new(new(minLat, minLng, maxLat - minLat, maxLng - minLng), new(minLat2, minLng2, maxLat2 - minLat2, maxLng2 - minLng2)));
	}

	public override void Deactivated() { }

	#region 上部時刻表示とか
	private bool _isWorking;
	public bool IsWorking
	{
		get => _isWorking;
		set => this.RaiseAndSetIfChanged(ref _isWorking, value);
	}

	private DateTime _currentTime = DateTime.Now;
	public DateTime CurrentTime
	{
		get => _currentTime;
		set => this.RaiseAndSetIfChanged(ref _currentTime, value);
	}

	private bool _isReplay;
	public bool IsReplay
	{
		get => _isReplay;
		set => this.RaiseAndSetIfChanged(ref _isReplay, value);
	}

	private bool _isSignalNowEewReceiving;
	public bool IsSignalNowEewReceiving
	{
		get => _isSignalNowEewReceiving;
		set => this.RaiseAndSetIfChanged(ref _isSignalNowEewReceiving, value);
	}

	private bool _isLast10SecondsEewReceiving;
	public bool IsLast10SecondsEewReceiving
	{
		get => _isLast10SecondsEewReceiving;
		set => this.RaiseAndSetIfChanged(ref _isLast10SecondsEewReceiving, value);
	}
	#endregion 上部時刻表示とか

	public bool IsDebug { get; }
#if DEBUG
		= true;
#endif

	/// <summary>
	/// 警告メッセージ
	/// </summary>
	private string? _warningMessage;
	public string? WarningMessage
	{
		get => _warningMessage;
		set => this.RaiseAndSetIfChanged(ref _warningMessage, value);
	}

	public Location? CurrentLocation
	{
		get => KyoshinMonitorLayer.CurrentLocation;
		set => KyoshinMonitorLayer.CurrentLocation = value;
	}

	private IEew[] _eews = Array.Empty<IEew>();
	public IEew[] Eews
	{
		get => _eews;
		set => this.RaiseAndSetIfChanged(ref _eews, value);
	}

	private IEnumerable<RealtimeObservationPoint>? _realtimePoints = Array.Empty<RealtimeObservationPoint>();
	public int RealtimePointCounts => RealtimePoints?.Count(p => p.LatestIntensity != null) ?? 0;
	public IEnumerable<RealtimeObservationPoint>? RealtimePoints
	{
		get => _realtimePoints;
		set {
			this.RaiseAndSetIfChanged(ref _realtimePoints, value);
			this.RaisePropertyChanged(nameof(RealtimePointCounts));
		}
	}

	private KyoshinEvent[] _kyoshinEvents = Array.Empty<KyoshinEvent>();
	public KyoshinEvent[] KyoshinEvents
	{
		get => _kyoshinEvents;
		set => this.RaiseAndSetIfChanged(ref _kyoshinEvents, value);
	}

	private RealtimeDataRenderMode _listRenderMode = RealtimeDataRenderMode.ShindoIcon;
	public RealtimeDataRenderMode ListRenderMode
	{
		get => _listRenderMode;
		set => this.RaiseAndSetIfChanged(ref _listRenderMode, value);
	}

	private bool _showEewAccuracy = false;
	public bool ShowEewAccuracy
	{
		get => _showEewAccuracy;
		set => this.RaiseAndSetIfChanged(ref _showEewAccuracy, value);
	}

	private DateTime WorkingTime { get; set; }
}
