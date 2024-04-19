using Avalonia.Controls;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Core;
using Splat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.MapEdit;

public class MapEditSeries : SeriesBase
{
	public static SeriesMeta MetaData { get; } = new(typeof(MapEditSeries), "mapedit", "マップ編集", new FontIconSource { Glyph = "\xf044", FontFamily = new FontFamily(Utils.IconFontName) }, false, "アプリで扱う地図上の項目を編集します。(開発向け機能)");

	public MapEditSeries() : base(MetaData)
	{
		SplatRegistrations.RegisterLazySingleton<MapEditSeries>();

		MapPadding = new(505, 0, 0, 0);
	}

	private MapEditView? _control;
	public override Control DisplayControl => _control ?? throw new InvalidOperationException("初期化前にコントロールが呼ばれています");


	public override void Activating()
	{
		if (_control != null)
			return;
		_control = new MapEditView
		{
			DataContext = this,
		};
	}
	public override void Deactivated() { }
}
