using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using System;

namespace KyoshinEewViewer.Series.Qzss;

public class QzssSeries : SeriesBase
{
	public static SeriesMeta MetaData { get; } = new(typeof(QzssSeries), "qzss", "災危通報", new FontIconSource { Glyph = "\xf7bf", FontFamily = new("IconFont") }, false, "\"みちびき\" から配信される防災情報を表示します。");

	public QzssSeries() : base(MetaData)
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
