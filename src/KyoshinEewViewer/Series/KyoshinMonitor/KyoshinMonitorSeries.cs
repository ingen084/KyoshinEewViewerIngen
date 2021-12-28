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
using ReactiveUI.Fody.Helpers;
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
	public KyoshinMonitorSeries(NotificationService? notificationService = null) : base("強震モニタ")
	{
		KyoshinMonitorLayer = new(this);
		NotificationService = notificationService ?? Locator.Current.GetService<NotificationService>() ?? throw new Exception("NotificationServiceの解決に失敗しました");
		EewControler = new(NotificationService);
		KyoshinMonitorWatcher = new(EewControler);
		SignalNowEewReceiver = new(EewControler, this);
		MapPadding = new Thickness(0, 0, 300, 0);

		#region dev用モック
		if (Design.IsDesignMode)
		{
#if DEBUG
			CurrentTime = DateTime.Now;

			IsWorking = true;
			IsSignalNowEewReceiving = true;
			IsLast10SecondsEewReceiving = false;

			WarningMessage = "これは けいこくめっせーじ じゃ！";

			var points = new List<RealtimeObservationPoint>()
			{
				new RealtimeObservationPoint(new ObservationPoint{ Region = "テスト", Name = "テスト" }) { LatestIntensity = 0.1, LatestColor = new SKColor(255, 0, 0, 255) },
				new RealtimeObservationPoint(new ObservationPoint{ Region = "テスト", Name = "テスト" }) { LatestIntensity = 0.2, LatestColor = new SKColor(0, 255, 0, 255) },
				new RealtimeObservationPoint(new ObservationPoint{ Region = "テスト", Name = "テスト" }) { LatestIntensity = 0.3, LatestColor = new SKColor(255, 0, 255, 255) },
				new RealtimeObservationPoint(new ObservationPoint{ Region = "テスト", Name = "テスト" }) { LatestIntensity = 0.4, LatestColor = new SKColor(255, 255, 0, 255) },
				new RealtimeObservationPoint(new ObservationPoint{ Region = "テスト", Name = "テスト" }) { LatestIntensity = 0.6, LatestColor = new SKColor(0, 255, 255, 255) },
				new RealtimeObservationPoint(new ObservationPoint{ Region = "テスト", Name = "テスト" }) { LatestIntensity = 0.7, LatestColor = new SKColor(255, 255, 255, 255) },
				new RealtimeObservationPoint(new ObservationPoint{ Region = "テスト", Name = "テスト" }) { LatestIntensity = 0.8, LatestColor = new SKColor(0, 0, 0, 255) },
				new RealtimeObservationPoint(new ObservationPoint{ Region = "テスト", Name = "テスト" }) { LatestIntensity = 1.0, LatestColor = new SKColor(255, 0, 0, 255) },
			};

			RealtimePoints = points.OrderByDescending(p => p.LatestIntensity ?? -1, null);

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

	private EewController EewControler { get; }
	private NotificationService NotificationService { get; }
	public KyoshinMonitorWatchService KyoshinMonitorWatcher { get; }
	private SignalNowFileWatcher SignalNowEewReceiver { get; }

	private KyoshinMonitorLayer KyoshinMonitorLayer { get; }

	private KyoshinMonitorView? control;
	public override Control DisplayControl => control ?? throw new InvalidOperationException("初期化前にコントロールが呼ばれています");

	public bool IsActivate { get; set; }

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
		EewControler.EewUpdated += e =>
		{
			var eews = e.eews.Where(e => !e.IsCancelled && e.UpdatedTime - WorkingTime < TimeSpan.FromMilliseconds(ConfigurationService.Current.Timer.Offset * 2));
			KyoshinMonitorLayer.CurrentEews = Eews = eews.ToArray();
		};
		KyoshinMonitorWatcher.RealtimeDataUpdated += e =>
		{
			//var parseTime = DateTime.Now - WorkStartedTime;

			RealtimePoints = e.data?.OrderByDescending(p => p.LatestIntensity ?? -1000, null);

			if (e.data != null)
				WarningMessage = null;
			//IsImage = e.IsUseAlternativeSource;
			IsWorking = false;
			CurrentTime = e.time;
			KyoshinMonitorLayer.ObservationPoints = e.data;
		};

		IsSignalNowEewReceiving = SignalNowEewReceiver.CanReceive;

		ConfigurationService.Current.Timer.WhenAnyValue(x => x.TimeshiftSeconds).Subscribe(x => IsReplay = x < 0);
		ConfigurationService.Current.KyoshinMonitor.WhenAnyValue(x => x.ListRenderMode)
			.Subscribe(x => ListRenderMode = Enum.TryParse<RealtimeDataRenderMode>(ConfigurationService.Current.KyoshinMonitor.ListRenderMode, out var mode) ? mode : ListRenderMode);
		ListRenderMode = Enum.TryParse<RealtimeDataRenderMode>(ConfigurationService.Current.KyoshinMonitor.ListRenderMode, out var mode) ? mode : ListRenderMode;

		Task.Run(() => KyoshinMonitorWatcher.Start());
	}

	public override void Deactivated() => IsActivate = false;


	#region 上部時刻表示とか
	[Reactive]
	public bool IsWorking { get; set; }

	[Reactive]
	public DateTime CurrentTime { get; set; } = DateTime.Now;

	[Reactive]
	public bool IsReplay { get; set; }

	[Reactive]
	public bool IsSignalNowEewReceiving { get; set; }
	[Reactive]
	public bool IsLast10SecondsEewReceiving { get; set; }
	#endregion 上部時刻表示とか

	#region 警告メッセージ

	public string? WarningMessage { get; set; }

	#endregion 警告メッセージ

	public Location? CurrentLocation
	{
		get => KyoshinMonitorLayer.CurrentLocation;
		set => KyoshinMonitorLayer.CurrentLocation = value;
	}

	[Reactive]
	public Eew[] Eews { get; set; } = Array.Empty<Eew>();

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

	[Reactive]
	public RealtimeDataRenderMode ListRenderMode { get; set; } = RealtimeDataRenderMode.ShindoIcon;

	private DateTime WorkingTime { get; set; }
}
