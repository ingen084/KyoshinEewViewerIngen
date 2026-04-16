using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Services;
using ReactiveUI;
using Splat;
using System;

namespace KyoshinEewViewer.Series.Lightning;

public class LightningSeries : SeriesBase
{
	public static SeriesMeta MetaData { get; } = new(typeof(LightningSeries), "lightning", "[TEST]落雷情報", new FAFontIconSource { Glyph = "\xf76c", FontFamily = new FontFamily(Utils.IconFontName) }, true, "落雷情報を表示します。(デバッグ用)");

	private SoundCategory SoundCategory { get; } = new("Lightning", "落雷情報");
	private Sound? ArrivalSound { get; set; }

	private LightningLayer Layer { get; }

	private LightningView? _control;
	public override Control DisplayControl => _control ?? throw new InvalidOperationException("初期化前にコントロールが呼ばれています");
	public override ISettingPage[] SettingPages => [];
	private LightningMapConnection Connection { get; } = new LightningMapConnection();

	private float _delay = 0;
	public float Delay
	{
		get => _delay;
		set => this.RaiseAndSetIfChanged(ref _delay, value);
	}

	public LightningSeries(SoundPlayerService soundPlayer, TimerService timer) : base(MetaData)
	{
		SplatRegistrations.RegisterLazySingleton<LightningSeries>();

		ArrivalSound = soundPlayer.RegisterSound(SoundCategory, "Arrival", "情報受信時");

		Layer = new LightningLayer(timer);

		MapDisplayParameter = new()
		{
			OverlayLayers = [Layer],
		};
	}

	public override void Initialize()
	{
		Connection.Arrived += e =>
		{
			if (e == null)
				return;
			ArrivalSound?.Play();
			Layer.Appear(DateTimeOffset.FromUnixTimeMilliseconds(e.Time / 1000000).DateTime, new KyoshinMonitorLib.Location(e.Lat, e.Lon));
			Delay = e.Delay;
		};
		// Connection.Connect();
	}

	public override Size MinViewSize { get; } = new(350, 450);

	public override void RecreateDisplayControl()
		=> _control = new LightningView { DataContext = this };
}
