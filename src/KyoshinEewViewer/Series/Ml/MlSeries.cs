using Avalonia.Controls;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Core;
using ReactiveUI;
using Splat;
using System;

namespace KyoshinEewViewer.Series.Ml;

public class MlSeries : SeriesBase
{
	public static SeriesMeta MetaData { get; } = new(typeof(MlSeries), "ml", "[実験]機械学習", new FontIconSource { Glyph = "\xf085", FontFamily = new FontFamily(Utils.IconFontName) }, true, "機械学習実験用シリーズ");

	private MlView? _control;
	public override Control DisplayControl => _control ?? throw new InvalidOperationException("初期化前にコントロールが呼ばれています");
	public override ISettingPage[] SettingPages => [];

	public MlSeries() : base(MetaData)
	{
		SplatRegistrations.RegisterLazySingleton<MlSeries>();
	}

	public override void Activating()
	{
		if (_control != null)
			return;
		_control = new MlView
		{
			DataContext = this,
		};

		MapDisplayParameter = new()
		{
			OverlayLayers = [],
		};
	}

	public override void Deactivated() { }
}
