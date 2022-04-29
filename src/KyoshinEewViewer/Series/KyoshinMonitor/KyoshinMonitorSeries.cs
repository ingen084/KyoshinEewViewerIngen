using Avalonia;
using Avalonia.Controls;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.CustomControl;
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
	public KyoshinMonitorSeries() : this(null)
	{ }
	public KyoshinMonitorSeries(NotificationService? notificationService) : base("強震モニタ")
	{
		KyoshinMonitorLayer = new(this);
		NotificationService = notificationService ?? Locator.Current.GetService<NotificationService>() ?? throw new Exception("NotificationServiceの解決に失敗しました");
		EewController = new(SoundCategory, NotificationService);
		KyoshinMonitorWatcher = new(EewController);
		SignalNowEewReceiver = new(EewController, this);
		MapPadding = new Thickness(0, 0, 300, 0);

		ShakeDetectedSound = SoundPlayerService.RegisterSound(SoundCategory, "WeakShakeDetected", "揺れ検出", "鳴動させるためには揺れ検出の設定を有効にしている必要があります。");

		#region dev用モック
		if (Design.IsDesignMode)
		{
#if DEBUG
			CurrentTime = DateTime.Now;
			IsDebug = true;

			IsWorking = true;
			IsSignalNowEewReceiving = true;
			IsLast10SecondsEewReceiving = false;

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
				new(DateTime.Now, new RealtimeObservationPoint(new ObservationPoint{ Region = "テスト", Name = "テスト", Point = new() }) { LatestIntensity = 0.1, LatestColor = new SKColor(255, 0, 0, 255) }),
				new(DateTime.Now, new RealtimeObservationPoint(new ObservationPoint{ Region = "テスト2", Name = "テスト2", Point = new() }) { LatestIntensity = 5.1, LatestColor = new SKColor(255, 0, 0, 255) }),
			};

			Eews = new[]
			{
				new Eew(EewSource.NIED, "a")
				{
					Intensity = JmaIntensity.Int3,
					IsWarning = false,
					ReceiveTime = DateTime.Now,
					OccurrenceTime = DateTime.Now,
					Depth = 0,
					Place = "通常テスト",
				},
				new Eew(EewSource.NIED, "b")
				{
					Intensity = JmaIntensity.Int5Lower,
					IsWarning = true,
					ReceiveTime = DateTime.Now,
					OccurrenceTime = DateTime.Now,
					Magnitude = 1.0f,
					Depth = 10,
					Place = "PLUMテスト",
				},
				new Eew(EewSource.NIED, "c")
				{
					Intensity = JmaIntensity.Unknown,
					IsWarning = false,
					IsCancelled = true,
					ReceiveTime = DateTime.Now,
					OccurrenceTime = DateTime.Now,
					Depth = 0,
					Place = "キャンセルテスト",
				}
			};

			IsReplay = true;
#endif
		}
		#endregion
	}

	public SoundPlayerService.SoundCategory SoundCategory { get; } = new("KyoshinMonitor", "強震モニタ");
	private SoundPlayerService.Sound ShakeDetectedSound { get; }

	private EewController EewController { get; }
	private NotificationService NotificationService { get; }
	public KyoshinMonitorWatchService KyoshinMonitorWatcher { get; }
	private SignalNowFileWatcher SignalNowEewReceiver { get; }

	private KyoshinMonitorLayer KyoshinMonitorLayer { get; }

	private KyoshinMonitorView? control;
	public override Control DisplayControl => control ?? throw new InvalidOperationException("初期化前にコントロールが呼ばれています");

	public bool IsActivate { get; set; }

	private Dictionary<Guid, KyoshinEventLevel> KyoshinEventLevelCache { get; } = new();

	public override void Activating()
	{
		IsActivate = true;
		if (control != null)
			return;

		control = new KyoshinMonitorView
		{
			DataContext = this
		};

		if (Design.IsDesignMode)
			return;

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
				var maxlv = e.events.Max(e => e.Level);
				foreach (var evt in e.events)
				{
					// 現時刻で検知、もしくはレベル上昇していれば音声を再生
					// ただし strong なイベント発生中は Weaker を扱わない
					if (
						(!KyoshinEventLevelCache.TryGetValue(evt.Id, out var lv) || lv < evt.Level) &&
						(maxlv < KyoshinEventLevel.Strong || evt.Level >= KyoshinEventLevel.Weak)
					)
						ShakeDetectedSound.Play();
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

		Task.Run(() => KyoshinMonitorWatcher.Start());
	}

	private void UpateFocusPoint(DateTime time)
	{
		if (!Eews.Any() && (!ConfigurationService.Current.KyoshinMonitor.UseExperimentalShakeDetect || !KyoshinEvents.Any()))
		{
			FocusBound = null;
			return;
		}

		// 自動ズーム範囲を計算
		var minLat = float.MaxValue;
		var maxLat = float.MinValue;
		var minLng = float.MaxValue;
		var maxLng = float.MinValue;

		// EEW
		// 震度が不明でない、キャンセルされてない、最終報から1分未満、座標が設定されている場合のみズーム
		foreach (var p in Eews
			.Where(e => e.Intensity != JmaIntensity.Unknown && !e.IsCancelled && (!e.IsFinal || (time - e.UpdatedTime).Minutes < 1) && e.Location != null)
			.SelectMany(e => new Location[] { new(e.Location!.Latitude - 1, e.Location.Longitude - 1), new(e.Location.Latitude + 1, e.Location.Longitude + 1) }))
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
		// Event
		foreach (var p in KyoshinEvents.SelectMany(e => new Location[] { new(e.TopLeft.Latitude - .5f, e.TopLeft.Longitude - .5f), new(e.BottomRight.Latitude + .5f, e.BottomRight.Longitude + .5f) }))
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

		var rect = new Rect(minLat, minLng, maxLat - minLat, maxLng - minLng);
		FocusBound = rect;
	}

	public override void Deactivated() => IsActivate = false;

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

	private Eew[] _eews = Array.Empty<Eew>();
	public Eew[] Eews
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

	private DateTime WorkingTime { get; set; }
}
