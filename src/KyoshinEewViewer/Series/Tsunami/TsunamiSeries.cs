using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using System;

namespace KyoshinEewViewer.Series.Tsunami;
public class TsunamiSeries : SeriesBase
{
	public TsunamiSeries() : base("津波情報", new FontIcon { Glyph = "\xe515", FontFamily = new("IconFont") })
	{
	}

	private TsunamiView? control;
	public override Control DisplayControl => control ?? throw new InvalidOperationException("初期化前にコントロールが呼ばれています");

	public override void Activating()
	{
		if (control != null)
			return;
		control = new TsunamiView
		{
			DataContext = this,
		};
	}
	public override void Deactivated() { }
}
