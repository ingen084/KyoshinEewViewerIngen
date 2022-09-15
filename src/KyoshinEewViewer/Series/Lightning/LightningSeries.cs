using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Services;
using ReactiveUI;
using System;

namespace KyoshinEewViewer.Series.Lightning;

public class LightningSeries : SeriesBase
{
	private SoundCategory SoundCategory { get; } = new("Lightning", "落雷情報");
	private Sound ArrivalSound { get; }

	public LightningSeries() : base("[TEST]落雷情報", new FontIcon { Glyph = "\xf76c", FontFamily = new("IconFont") })
	{
		ArrivalSound = SoundPlayerService.RegisterSound(SoundCategory, "Arrival", "情報受信時");
	}
	private LightningLayer Layer { get; } = new();

	private LightningView? control;
	public override Control DisplayControl => control ?? throw new InvalidOperationException("初期化前にコントロールが呼ばれています");
	private LightningMapConnection Connection { get; } = new LightningMapConnection();

	private float _delay = 0;
	public float Delay
	{
		get => _delay;
		set => this.RaiseAndSetIfChanged(ref _delay, value);
	}

	public override void Activating()
	{
		if (control != null)
			return;
		control = new LightningView
		{
			DataContext = this,
		};

		OverlayLayers = new[] { Layer };

		Connection.Arrived += e =>
		{
			if (e == null)
				return;
			ArrivalSound.Play();
			Layer.Appear(DateTimeOffset.FromUnixTimeMilliseconds(e.Time / 1000000).DateTime, new KyoshinMonitorLib.Location(e.Lat, e.Lon));
			Delay = e.Delay;
		};
		// Connection.Connect();
	}
	public override void Deactivated() { }
}
