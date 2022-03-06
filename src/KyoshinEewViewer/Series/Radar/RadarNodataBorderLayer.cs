using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Layers;
using KyoshinEewViewer.Map.Simplify;
using KyoshinEewViewer.Series.Radar.Models;
using KyoshinMonitorLib;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Series.Radar;

public class RadarNodataBorderLayer : MapLayer
{
	private static readonly SKPaint BorderPen = new()
	{
		Style = SKPaintStyle.Stroke,
		StrokeWidth = 2,
		Color = SKColors.Gray.WithAlpha(100),
	};

	private List<Location[]> Points { get; } = new();
	private SKPath? PathCache { get; set; }
	private int CachedZoom { get; set; }
	private bool NeedUpdate { get; set; }

	private bool IsDisposed { get; set; }

	public override bool NeedPersistentUpdate => false;

	public void UpdatePoints(GeoJson baseJson)
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
		if (coordinates.Length < 2)
			throw new Exception("配列数が不正です: " + coordinates.Length);

		Points.Clear();
		foreach (var c in coordinates[1..])
		{
			var points = new List<Location>();
			foreach (var ps in c)
			{
				if (ps.Length != 2)
					continue;
				points.Add(new Location(ps[1], ps[0]));
			}
			Points.Add(points.ToArray());
		}
		NeedUpdate = true;
	}

	public override void RefreshResourceCache(Avalonia.Controls.Control targetControl) { }

	public override void Render(SKCanvas canvas, bool isAnimating)
	{
		if (IsDisposed)
			return;
		canvas.Save();
		try
		{
			canvas.Translate((float)-LeftTopPixel.X, (float)-LeftTopPixel.Y);

			// 使用するキャッシュのズーム
			var baseZoom = (int)Zoom;
			// 実際のズームに合わせるためのスケール
			var scale = Math.Pow(2, Zoom - baseZoom);
			canvas.Scale((float)scale);

			BorderPen.StrokeWidth = (float)(2 / scale);

			if (NeedUpdate || baseZoom != CachedZoom)
			{
				CreateGeometry(baseZoom);
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

	private void CreateGeometry(int zoom)
	{
		// いろいろ非同期でジオメトリ生成中でもDisposeされる可能性がある
		var path = new SKPath
		{
			FillType = SKPathFillType.EvenOdd
		};
		foreach (var s in Points.ToArray())
		{
			var points = DouglasPeucker.Reduction(s.Select(p => p.ToPixel(zoom)).ToArray(), 1, true);
			if (points.Length > 2)
				path.AddPoly(points);
		}
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
