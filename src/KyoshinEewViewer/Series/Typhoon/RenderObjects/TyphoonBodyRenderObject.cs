using KyoshinEewViewer.Map;
using KyoshinEewViewer.Series.Typhoon.Models;
using KyoshinMonitorLib;
using SkiaSharp;
using System;

namespace KyoshinEewViewer.Series.Typhoon.RenderObjects;

public class TyphoonBodyRenderObject : IDisposable
{
	private static readonly SKPaint CenterPaint = new()
	{
		Style = SKPaintStyle.Stroke,
		Color = SKColors.MediumAquamarine,
		StrokeWidth = 2,
		IsAntialias = true,
	};
	private static readonly SKPaint StrongPaint = new()
	{
		Style = SKPaintStyle.Stroke,
		Color = SKColors.Yellow,
		StrokeWidth = 2,
		IsAntialias = true,
	};
	private static readonly SKPaint StormPaint = new()
	{
		Style = SKPaintStyle.Stroke,
		Color = SKColors.Crimson,
		StrokeWidth = 2,
		IsAntialias = true,
	};
	private static readonly SKPaint StrongFillPaint = new()
	{
		Style = SKPaintStyle.Fill,
		Color = SKColors.Yellow.WithAlpha(50),
		StrokeWidth = 2,
	};
	private static readonly SKPaint StormFillPaint = new()
	{
		Style = SKPaintStyle.Fill,
		Color = SKColors.Crimson.WithAlpha(50),
		StrokeWidth = 2,
	};

	public bool IsBaseMode { get; set; }

	private const int CacheZoom = 5;
	private SKPath? StrongCache { get; set; }
	private SKPath? StormCache { get; set; }

	public Location OriginLocation { get; }
	public TyphoonRenderCircle? StrongCircle { get; }
	public TyphoonRenderCircle? StormCircle { get; }

	public TyphoonBodyRenderObject(TyphoonPlace place, bool isBaseMode)
	{
		OriginLocation = place.Center;
		StrongCircle = place.Strong;
		StormCircle = place.Storm;
		IsBaseMode = isBaseMode;
	}

	public void Render(SKCanvas canvas, double zoom)
	{
		if (IsDisposed)
			return;

		// 実際のズームに合わせるためのスケール
		var scale = Math.Pow(2, zoom - CacheZoom);

		if (IsBaseMode)
		{
			CenterPaint.StrokeWidth = (float)(2 / scale);
			canvas.DrawCircle(OriginLocation.ToPixel(CacheZoom).AsSKPoint(), (float)(3 / scale), CenterPaint);
			return;
		}
		// 強風域
		if (StrongCircle != null)
		{
			StrongPaint.StrokeWidth = (float)(2 / scale);
			if (StrongCache == null)
				StrongCache = PathGenerator.MakeCirclePath(StrongCircle.RawCenter, StrongCircle.RangeKilometer * 1000, CacheZoom, 90);

			canvas.DrawPath(StrongCache, StrongFillPaint);
			canvas.DrawPath(StrongCache, StrongPaint);
		}

		// 暴風域
		if (StormCircle != null)
		{
			StormPaint.StrokeWidth = (float)(2 / scale);
			if (StormCache == null)
				StormCache = PathGenerator.MakeCirclePath(StormCircle.RawCenter, StormCircle.RangeKilometer * 1000, CacheZoom, 90);

			canvas.DrawPath(StormCache, StormFillPaint);
			canvas.DrawPath(StormCache, StormPaint);
		}

		CenterPaint.StrokeWidth = (float)(2 / scale);
		var p = OriginLocation.ToPixel(CacheZoom);
		var size = 5 / scale;
		canvas.DrawLine((p + new PointD(-size, -size)).AsSKPoint(), (p + new PointD(size, size)).AsSKPoint(), CenterPaint);
		canvas.DrawLine((p + new PointD(size, -size)).AsSKPoint(), (p + new PointD(-size, size)).AsSKPoint(), CenterPaint);
	}

	private bool IsDisposed { get; set; }

	public void Dispose()
	{
		IsDisposed = true;

		StrongCache?.Dispose();
		StrongCache = null;

		StormCache?.Dispose();
		StormCache = null;

		GC.SuppressFinalize(this);
	}
}
