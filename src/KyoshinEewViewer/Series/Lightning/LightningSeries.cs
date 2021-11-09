using Avalonia.Controls;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;

namespace KyoshinEewViewer.Series.Lightning;

public class LightningSeries : SeriesBase
{
	public LightningSeries() : base("[TEST]落雷情報")
	{
	}
	private List<LightningRealtimeRenderObject> LightningCache { get; } = new();
	private TimeSpan DeleteTime { get; } = TimeSpan.FromSeconds(20);

	private LightningView? control;
	public override Control DisplayControl => control ?? throw new InvalidOperationException("初期化前にコントロールが呼ばれています");
	private LightningMapConnection Connection { get; } = new LightningMapConnection();

	[Reactive]
	public int ActivesCount { get; set; } = 0;
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

		Connection.Arrived += e =>
		{
			if (e == null)
				return;
			LightningCache.Insert(0, new LightningRealtimeRenderObject(DateTimeOffset.FromUnixTimeMilliseconds(e.Time / 1000000).LocalDateTime, DateTime.Now, new KyoshinMonitorLib.Location(e.Lat, e.Lon)));
			LightningCache.RemoveAll(l => l.TimeOffset >= DeleteTime);
			ActivesCount = LightningCache.Count;
			Delay = e.Delay;
			RealtimeRenderObjects = LightningCache.ToArray();
		};
		Connection.Connect();
	}
	public override void Deactivated() { }
}
