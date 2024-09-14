using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Events;
using KyoshinEewViewer.Series.KyoshinMonitor.Events;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;
using KyoshinEewViewer.Series.KyoshinMonitor.Workflow;
using KyoshinEewViewer.Services;
using ReactiveUI;
using Splat;
using System;
using System.Linq;

namespace KyoshinEewViewer.Series.KyoshinMonitor;

public class KyoshinMonitorSeries : SeriesBase
{
	public static SeriesMeta MetaData { get; } = new(typeof(KyoshinMonitorSeries), "kyoshin-monitor", "強震モニタ", new FontIconSource { Glyph = "\xe3b1", FontFamily = new(Utils.IconFontName) }, true, "強震モニタ･緊急地震速報を表示します。");

	public SoundCategory SoundCategory { get; } = new("KyoshinMonitor", "強震モニタ");
	private Sound WeakShakeDetectedSound { get; set; }
	private Sound MediumShakeDetectedSound { get; set; }
	private Sound StrongShakeDetectedSound { get; set; }
	private Sound StrongerShakeDetectedSound { get; set; }

	private NotificationService NotificationService { get; }
	private WorkflowService WorkflowService { get; }
	private KyoshinEewViewerConfiguration Config { get; }

	private KyoshinMonitorLayer KyoshinMonitorLayer { get; set; }

	private KyoshinMonitorView? _control;
	public override Control DisplayControl => _control ?? throw new InvalidOperationException("初期化前にコントロールが呼ばれています");

	private RealtimeEarthquakeInformationHost RealtimeInformationHost { get; }

	private IDisposable? MapNavigationSubscription { get; set; }
	private IDisposable? MapDisplayParameterSubscription { get; set; }

	private EarthquakeInformationHost? _currentInformationHost;
	public EarthquakeInformationHost CurrentInformationHost
	{
		get => _currentInformationHost ?? throw new InvalidOperationException("初期化前に CurrentInformationHost が呼ばれています");
		set {
			if (_currentInformationHost == value)
				return;

			if (_currentInformationHost != null)
			{
				_currentInformationHost.EewUpdated -= EewUpdated;
				_currentInformationHost.RealtimeDataUpdated -= RealtimeDataUpdated;
				_currentInformationHost.KyoshinEventUpdated -= KyoshinEventUpdated;
			}
			this.RaiseAndSetIfChanged(ref _currentInformationHost, value);

			value.EewUpdated += EewUpdated;
			value.RealtimeDataUpdated += RealtimeDataUpdated;
			value.KyoshinEventUpdated += KyoshinEventUpdated;

			MapNavigationSubscription?.Dispose();
			MapNavigationSubscription = value.WhenAnyValue(x => x.MapNavigationRequest).Subscribe(x => MapNavigationRequest = x);

			MapDisplayParameterSubscription?.Dispose();
			MapDisplayParameterSubscription = value.WhenAnyValue(x => x.MapDisplayParameter).Subscribe(x => MapDisplayParameter = x with { OverlayLayers = [KyoshinMonitorLayer] });
		}
	}

	public DateTime CurrentDisplayTime => _currentInformationHost?.CurrentTime ?? DateTime.Now;

	public KyoshinMonitorSeries(
		KyoshinEewViewerConfiguration config,
		NotificationService notifyService,
		SoundPlayerService soundPlayer,
		WorkflowService workflowService,
		RealtimeEarthquakeInformationHost realtimeHost) : base(MetaData)
	{
		SplatRegistrations.RegisterLazySingleton<KyoshinMonitorSeries>();

		NotificationService = notifyService;
		Config = config;
		WorkflowService = workflowService;

		WeakShakeDetectedSound = soundPlayer.RegisterSound(SoundCategory, "WeakShakeDetected", "揺れ検出(震度1未満)", "鳴動させるためには揺れ検出の設定を有効にしている必要があります。");
		MediumShakeDetectedSound = soundPlayer.RegisterSound(SoundCategory, "MediumShakeDetected", "揺れ検出(震度1以上3未満)", "震度上昇時にも鳴動します。\n鳴動させるためには揺れ検出の設定を有効にしている必要があります。");
		StrongShakeDetectedSound = soundPlayer.RegisterSound(SoundCategory, "StrongShakeDetected", "揺れ検出(震度3以上5弱未満)", "震度上昇時にも鳴動します。\n鳴動させるためには揺れ検出の設定を有効にしている必要があります。");
		StrongerShakeDetectedSound = soundPlayer.RegisterSound(SoundCategory, "StrongerShakeDetected", "揺れ検出(震度5弱以上)", "震度上昇時にも鳴動します。\n鳴動させるためには揺れ検出の設定を有効にしている必要があります。");

		CurrentInformationHost = RealtimeInformationHost = realtimeHost;

		KyoshinMonitorLayer = new(config, this);
		MapDisplayParameter = new() { OverlayLayers = [KyoshinMonitorLayer] };

		config.Eew.WhenAnyValue(x => x.ShowDetails).Subscribe(x => ShowEewAccuracy = x);
		config.KyoshinMonitor.WhenAnyValue(x => x.ShowColorSample).Subscribe(x => ShowColorSample = x);
	}
	public override void Initialize()
		=> RealtimeInformationHost.StartAsync();

	public override void Activating()
	{
		if (_control != null)
			return;

		_control = new KyoshinMonitorView
		{
			DataContext = this
		};
	}

	public void EewUpdated(DateTime updatedTime, IEew[] eews)
		=> KyoshinMonitorLayer.CurrentEews = eews.Where(eew => eew.IsVisible)
			.OrderByDescending(eew => eew.OccurrenceTime).ToArray();

	public void RealtimeDataUpdated((DateTime time, RealtimeObservationPoint[] data, KyoshinEvent[] events) e)
	{
		KyoshinMonitorLayer.ObservationPoints = e.data;
		KyoshinMonitorLayer.KyoshinEvents = e.events;
	}

	public void KyoshinEventUpdated((DateTime time, KyoshinEvent e, bool isLevelUp) e)
	{
		WorkflowService.PublishEvent(new ShakeDetectedEvent(e.time, e.e));

		switch (e.e.Level)
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
		MessageBus.Current.SendMessage(new KyoshinShakeDetected(e.e, e.isLevelUp));
		if (Config.KyoshinMonitor.SwitchAtShakeDetect && e.e.Level >= KyoshinEventLevel.Weak)
			ActiveRequest.Send(this);
	}

	public bool IsDebug { get; }
#if DEBUG
		= true;
#endif

	private bool _showColorSample;
	public bool ShowColorSample
	{
		get => _showColorSample;
		set => this.RaiseAndSetIfChanged(ref _showColorSample, value);
	}

	private bool _showEewAccuracy = false;
	public bool ShowEewAccuracy
	{
		get => _showEewAccuracy;
		set => this.RaiseAndSetIfChanged(ref _showEewAccuracy, value);
	}

	public override void Deactivated() { }
}
