using Avalonia.Controls;
using KyoshinEewViewer.Map.Layers;
using KyoshinEewViewer.Series.Typhoon.Models;
using KyoshinEewViewer.Series.Typhoon.RenderObjects;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

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
			RefleshRequest();
		}
	}

	private Dictionary<string, TyphoonRenderCache> RenderCaches { get; } = new();

	private const int CacheZoom = 5;
	private sealed record TyphoonRenderCache(TyphoonBodyRenderObject[] Bodies, TyphoonForecastRenderObject Forecast) : IDisposable
	{
		public void Dispose()
		{
			foreach (var body in Bodies)
				body.Dispose();
			Forecast?.Dispose();
		}
	}

	public override bool NeedPersistentUpdate => false;

	public override void RefreshResourceCache(Control targetControl) { }
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
						cache = new(
							new[] { new TyphoonBodyRenderObject(item.CurrentPlace, false) }.Concat(
								item.Places.Select(p => new TyphoonBodyRenderObject(p, true))
							).ToArray(),
							new(item.CurrentPlace, item.Places)
						);
					}
					cache.Forecast.Render(canvas, param.Zoom);
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
