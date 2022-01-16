using Avalonia.Controls;
using KyoshinEewViewer.Services;
using ReactiveUI.Fody.Helpers;
using System;

namespace KyoshinEewViewer.Series.Lightning;

public class LightningSeries : SeriesBase
{
	private SoundPlayerService.SoundCategory SoundCategory { get; } = new("Lightning", "落雷情報");
	private SoundPlayerService.Sound ArrivalSound { get; }

	public LightningSeries() : base("[TEST]落雷情報")
	{
		ArrivalSound = SoundPlayerService.RegisterSound(SoundCategory, "Arrival", "情報受信時");
	}
	private LightningLayer Layer { get; } = new();

	private LightningView? control;
	public override Control DisplayControl => control ?? throw new InvalidOperationException("初期化前にコントロールが呼ばれています");
	private LightningMapConnection Connection { get; } = new LightningMapConnection();

	[Reactive]
	public float Delay { get; set; } = 0;

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
			Layer.Appear(DateTimeOffset.FromUnixTimeMilliseconds(e.Time / 1000000).LocalDateTime, new KyoshinMonitorLib.Location(e.Lat, e.Lon));
			Delay = e.Delay;
		};
		Connection.Connect();
	}
	public override void Deactivated() { }
}
