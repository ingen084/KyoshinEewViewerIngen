using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Layers;
using KyoshinEewViewer.Series.Typhoon.Models;
using KyoshinEewViewer.Series.Typhoon.RenderObjects;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using Location = KyoshinMonitorLib.Location;

namespace KyoshinEewViewer.Series.Typhoon;

public class TyphoonLayer : MapLayer
{
	private TyphoonItem[]? _typhoonItems;
	public TyphoonItem[]? TyphoonItems
	{
		get => _typhoonItems;
		set {
			_typhoonItems = value;
			// キャッシュをクリアしておく
			lock (RenderCaches)
			{
				foreach (var kvp in RenderCaches)
					kvp.Value.Dispose();
				RenderCaches.Clear();
			}
			RefreshRequest();
		}
	}

	private static readonly SKPaint HistoryPaint = new()
	{
		Style = SKPaintStyle.Stroke,
		Color = SKColors.Gray.WithAlpha(200),
		StrokeWidth = 2,
		IsAntialias = true,
	};

	private Dictionary<string, TyphoonRenderCache> RenderCaches { get; } = [];

	private const int CacheZoom = 5;
	private sealed record TyphoonRenderCache(TyphoonBodyRenderObject[] Bodies, TyphoonForecastRenderObject? Forecast) : IDisposable
	{
		public void Dispose()
		{
			foreach (var body in Bodies)
				body.Dispose();
			Forecast?.Dispose();
		}
	}

	public override bool NeedPersistentUpdate => false;

	public override void RefreshResourceCache(WindowTheme windowTheme) { }
	public override void Render(SKCanvas canvas, LayerRenderParameter param, bool isAnimating)
	{
		if (TyphoonItems == null)
			return;

		canvas.Save();
		try
		{
			canvas.Translate((float)-param.LeftTopPixel.X, (float)-param.LeftTopPixel.Y);
			// 実際のズームに合わせるためのスケール
			var scale = Math.Pow(2, param.Zoom - CacheZoom);
			canvas.Scale((float)scale);

			lock (RenderCaches)
			{
				foreach (var item in TyphoonItems)
				{
					if (!RenderCaches.TryGetValue(item.Id, out var cache))
					{
						var bodies = new List<TyphoonBodyRenderObject>
						{
							new(item.Current, false)
						};
						if (item.ForecastPlaces != null)
							bodies.AddRange(item.ForecastPlaces.Select(p => new TyphoonBodyRenderObject(p, true)));
						cache = new(
							bodies.ToArray(),
							item.ForecastPlaces == null ? null : new(item.Current, item.ForecastPlaces)
						);
					}

					if (item.LocationHistory != null)
					{
						HistoryPaint.StrokeWidth = (float)(2 / scale);
						Location? before = null;
						foreach (var p in item.LocationHistory)
						{
							if (before != null)
								canvas.DrawLine(before.ToPixel(CacheZoom).AsSkPoint(), p.ToPixel(CacheZoom).AsSkPoint(), HistoryPaint);
							before = p;
						}
					}

					cache.Forecast?.Render(canvas, param.Zoom);
					foreach (var body in cache.Bodies)
						body.Render(canvas, param.Zoom);
				}
			}
		}
		finally
		{
			canvas.Restore();
		}
	}
}
