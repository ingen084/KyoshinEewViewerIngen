using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using System;

namespace KyoshinEewViewer.Series.Qzss;

public class QzssSeries : SeriesBase
{
	public QzssSeries() : base("災危通報", new FontIconSource { Glyph = "\xf7bf", FontFamily = new("IconFont") })
	{
		MapPadding = new(255, 0, 0, 0);
	}

	private QzssView? control;
	public override Control DisplayControl => control ?? throw new InvalidOperationException("初期化前にコントロールが呼ばれています");

	public CurrentPositionLayer CurrentPositionLayer { get; } = new();

	public override void Activating()
	{
		if (control != null)
			return;
		control = new QzssView
		{
			DataContext = this,
		};

		CurrentPositionLayer.Location = new(34.6366f, 135.3708f);
		OverlayLayers = new[] {
			CurrentPositionLayer,
		};
	}
	public override void Deactivated() { }
}
