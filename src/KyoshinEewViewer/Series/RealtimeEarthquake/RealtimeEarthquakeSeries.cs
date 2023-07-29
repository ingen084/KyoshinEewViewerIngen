using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Core;
using System;
using Splat;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.RealtimeEarthquake;

public class RealtimeEarthquakeSeries : SeriesBase
{
	public static SeriesMeta MetaData { get; } = new(typeof(RealtimeEarthquakeSeries), "realtime-earthquake", "ﾘｱﾙﾀｲﾑ地震", new FontIconSource { Glyph = "\xe3b1", FontFamily = new(Utils.IconFontName) }, true, "緊急地震速報を表示します。強震モニタが有効な場合、自動で無効化されます。");

	public RealtimeEarthquakeSeries() : base(MetaData)
	{
		SplatRegistrations.RegisterLazySingleton<RealtimeEarthquakeSeries>();
	}

	public override Control DisplayControl => throw new NotImplementedException();

	public override void Activating() => throw new NotImplementedException();
	public override void Deactivated() => throw new NotImplementedException();
}
