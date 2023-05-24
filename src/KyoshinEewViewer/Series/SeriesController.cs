using KyoshinEewViewer.Core.Models;
using Splat;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace KyoshinEewViewer.Series;

/// <summary>
/// Series をまとめる
/// </summary>
public class SeriesController
{
	private List<SeriesMeta> Series { get; } = new();
	public IReadOnlyList<SeriesMeta> AllSeries => Series;

	public ObservableCollection<SeriesBase> EnabledSeries { get; } = new();

	public SeriesController()
	{
		SplatRegistrations.RegisterLazySingleton<SeriesController>();
	}

	public void RegisterSeries(SeriesMeta series)
	{
		if (Series.Any(s => s.Key == series.Key))
			throw new ArgumentException($"Key {series.Key} はすでに登録されています", nameof(series));
		Series.Add(series);
	}

	public void InitializeSeries(KyoshinEewViewerConfiguration config)
	{
		if (EnabledSeries.Any())
			throw new InvalidOperationException("すでに初期化されています");

		foreach(var meta in Series.Where(s => config.SeriesEnable.TryGetValue(s.Key, out var e) ? e : s.IsDefaultEnabled))
		{
			var series = Locator.Current.GetService(meta.Type) as SeriesBase ?? throw new InvalidOperationException($"{meta.Key} の初期化に失敗しました");
			series.Initialize();
			EnabledSeries.Add(series);
		}
	}
}
