using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Core;
using Splat;
using System;
using Location = KyoshinMonitorLib.Location;

namespace KyoshinEewViewer.Series.Qzss;

public class QzssSeries : SeriesBase
{
	public static SeriesMeta MetaData { get; } = new(typeof(QzssSeries), "qzss", "災危通報", new FontIconSource { Glyph = "\xf7bf", FontFamily = new FontFamily(Utils.IconFontName) }, false, "\"みちびき\" から配信される防災情報を表示します。");

	public QzssSeries() : base(MetaData)
	{
		SplatRegistrations.RegisterLazySingleton<QzssSeries>();

		MapPadding = new Thickness(255, 0, 0, 0);
	}

	private QzssView? _control;
	public override Control DisplayControl => _control ?? throw new InvalidOperationException("初期化前にコントロールが呼ばれています");

	public CurrentPositionLayer CurrentPositionLayer { get; } = new();

	public override void Activating()
	{
		if (_control != null)
			return;
		_control = new QzssView
		{
			DataContext = this,
		};

		CurrentPositionLayer.Location = new Location(34.6366f, 135.3708f);
		OverlayLayers = new[] {
			CurrentPositionLayer,
		};
	}
	public override void Deactivated() { }
}
