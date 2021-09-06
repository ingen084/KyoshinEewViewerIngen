using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Projections;
using KyoshinEewViewer.Series.Radar.Models;
using KyoshinMonitorLib;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Series.Radar.RenderObjects
{
	public class RadarNodataBorderRenderObject : IRenderObject, IDisposable
	{
		private static readonly SKPaint BorderPen = new()
		{
			Style = SKPaintStyle.Stroke,
			StrokeWidth = 2,
			Color = SKColors.Gray.WithAlpha(100),
		};

		private Location[] Points { get; }
		private SKPath? PathCache { get; set; }
		private int CachedZoom { get; set; }
		private bool NeedUpdate { get; set; }

		private bool IsDisposed { get; set; }

		public RadarNodataBorderRenderObject(GeoJson baseJson)
		{
			if (baseJson.Type != GeoJsonFeatureType.FeatureCollection)
				throw new Exception($"root が {nameof(GeoJsonFeatureType.FeatureCollection)} ではありません");

			var feature = (baseJson.Features ?? throw new Exception($"{nameof(baseJson.Features)} が null です")).FirstOrDefault();
			if (feature?.Type != GeoJsonFeatureType.Feature)
				throw new Exception($"Features[0] が {nameof(GeoJsonFeatureType.Feature)} ではありません");

			var geometry = feature.Geometry ?? throw new Exception($"{nameof(feature.Geometry)} が null です");
			if (geometry.Type != GeoJsonFeatureType.Polygon)
				throw new Exception($"Features[0].Geometry が {nameof(GeoJsonFeatureType.Polygon)} ではありません");

			var coordinates = geometry.Coordinates ?? throw new Exception($"{nameof(geometry.Coordinates)} が null です");
			if (coordinates.Length != 2)
				throw new Exception("配列数が不正です");

			var points = new List<Location>();
			foreach (var ps in coordinates[1])
			{
				if (ps.Length != 2)
					continue;
				points.Add(new Location(ps[1], ps[0]));
			}
			Points = points.ToArray();
			NeedUpdate = true;
		}

		public void Render(SKCanvas canvas, RectD viewRect, double zoom, PointD leftTopPixel, bool isAnimating, bool isDarkTheme, MapProjection projection)
		{
			if (IsDisposed)
				return;
			canvas.Save();
			try
			{
				canvas.Translate((float)-leftTopPixel.X, (float)-leftTopPixel.Y);

				// 使用するキャッシュのズーム
				var baseZoom = (int)zoom;
				// 実際のズームに合わせるためのスケール
				var scale = Math.Pow(2, zoom - baseZoom);
				canvas.Scale((float)scale);

				BorderPen.StrokeWidth = (float)(2 / scale);

				if (NeedUpdate || baseZoom != CachedZoom)
				{
					CreateGeometry(baseZoom, projection);
					CachedZoom = baseZoom;
					NeedUpdate = false;
				}

				if (PathCache != null)
					canvas.DrawPath(PathCache, BorderPen);
			}
			finally
			{
				canvas.Restore();
			}
		}

		private void CreateGeometry(int zoom, MapProjection proj)
		{
			// いろいろ非同期でジオメトリ生成中でもDisposeされる可能性がある
			var path = new SKPath();
			path.AddPoly(DouglasPeucker.Reduction(Points.Select(p => p.ToPixel(proj, zoom)).ToArray(), 1.5, true));
			if (!IsDisposed)
				PathCache = path;
			else
				path.Dispose();
		}

		public void Dispose()
		{
			IsDisposed = true;
			PathCache?.Dispose();
			PathCache = null;
			GC.SuppressFinalize(this);
		}
	}
}
