using KyoshinEewViewer.Map;
using KyoshinEewViewer.Series.Typhoon.Models;
using KyoshinMonitorLib;
using SkiaSharp;
using System;

namespace KyoshinEewViewer.Series.Typhoon.RenderObjects;

public class TyphoonForecastRenderObject : IRenderObject, IDisposable
{
	private static readonly SKPaint ForecastCirclePaint = new()
	{
		Style = SKPaintStyle.Stroke,
		Color = SKColors.White,
		StrokeWidth = 2,
		IsAntialias = true,
	};
	private static readonly SKPaint ForecastLinePaint = new()
	{
		Style = SKPaintStyle.Stroke,
		Color = SKColors.White,
		StrokeWidth = 2,
		IsAntialias = true,
	};
	private static readonly SKPaint StormPaint = new()
	{
		Style = SKPaintStyle.Stroke,
		Color = SKColors.Crimson,
		StrokeWidth = 1,
		IsAntialias = true,
	};

	public TyphoonForecastRenderObject(Location startLocation, TyphoonCircle? currentStormCircle, (TyphoonCircle? forecast, TyphoonCircle? storm)[] circles)
	{
		StartLocation = startLocation;
		CurrentStormCircle = currentStormCircle;
		Circles = circles ?? throw new ArgumentNullException(nameof(circles));
	}

	private Location StartLocation { get; }
	private TyphoonCircle? CurrentStormCircle { get; }
	private (TyphoonCircle? forecast, TyphoonCircle? storm)[] Circles { get; }

	private const int CacheZoom = 5;

	private SKPath? ForecastLineCache { get; set; }
	private SKPath? ForecastStormAreaCache { get; set; }
	private SKPath? ForecastCirclesCache { get; set; }

	public void Render(SKCanvas canvas, RectD viewRect, double zoom, PointD leftTopPixel, bool isAnimating, bool isDarkTheme)
	{
		if (IsDisposed)
			return;

		canvas.Save();
		try
		{
			canvas.Translate((float)-leftTopPixel.X, (float)-leftTopPixel.Y);
			// 実際のズームに合わせるためのスケール
			var scale = Math.Pow(2, zoom - CacheZoom);
			canvas.Scale((float)scale);

			ForecastLinePaint.StrokeWidth = (float)(2 / scale);
			// 予想を結ぶ線
			if (ForecastLineCache == null || ForecastStormAreaCache == null)
			{
				// 起点となる点
				ForecastLineCache = new();
				ForecastStormAreaCache = new();

				// 現在暴風域が存在する場合組み込んでおく
				if (CurrentStormCircle != null)
					PathGenerator.MakeCirclePath(CurrentStormCircle.RawCenter, CurrentStormCircle.RangeKilometer * 1000, CacheZoom, 90, ForecastStormAreaCache);

				(TyphoonCircle? forecast, TyphoonCircle? storm) beforeCircle = (new TyphoonCircle(StartLocation, 0, StartLocation), CurrentStormCircle);
				foreach (var circle in Circles)
				{
					// 強風域
					if (circle.forecast != null && beforeCircle.forecast != null &&
						GetSharedCircumscribedCrossPoints(beforeCircle.forecast, circle.forecast) is (Location s, Location e)[] flines)
					{
						foreach (var (s, e) in flines)
							ForecastLineCache.AddPoly(new[] {
									s.ToPixel(CacheZoom).AsSKPoint(),
									e.ToPixel(CacheZoom).AsSKPoint(),
								}, false);
					}

					// 暴風域
					if (circle.storm != null)
					{
						using var a = PathGenerator.MakeCirclePath(circle.storm.RawCenter, circle.storm.RangeKilometer * 1000, CacheZoom, 90);
						ForecastStormAreaCache.Op(a, SKPathOp.Union, ForecastStormAreaCache);
					}
					if (circle.storm != null && beforeCircle.storm != null &&
						GetSharedCircumscribedCrossPoints(beforeCircle.storm, circle.storm) is (Location s, Location e)[] slines)
					{
						if (slines.Length != 2)
							continue;
						// 台形のポリゴンを作成して合成
						using var a = new SKPath();
						a.AddPoly(new[] {
								slines[0].s.ToPixel(CacheZoom).AsSKPoint(),
								slines[0].e.ToPixel(CacheZoom).AsSKPoint(),
								slines[1].e.ToPixel(CacheZoom).AsSKPoint(),
								slines[1].s.ToPixel(CacheZoom).AsSKPoint(),
							}, true);
						ForecastStormAreaCache.Op(a, SKPathOp.Union, ForecastStormAreaCache);
					}

					beforeCircle = circle;
				}
			}
			canvas.DrawPath(ForecastLineCache, ForecastLinePaint);

			ForecastCirclePaint.StrokeWidth = (float)(2 / scale);
			ForecastCirclePaint.PathEffect = SKPathEffect.CreateDash(new[] { (float)(5 / scale), (float)(5 / scale) }, 1);
			if (ForecastCirclesCache == null)
			{
				ForecastCirclesCache = new SKPath();
				foreach (var (forecast, _) in Circles)
				{
					if (forecast == null)
						continue;
					PathGenerator.MakeCirclePath(forecast.RawCenter, forecast.RangeKilometer * 1000, CacheZoom, 90, ForecastCirclesCache);
				}
			}
			canvas.DrawPath(ForecastCirclesCache, ForecastCirclePaint);

			StormPaint.StrokeWidth = (float)(1 / scale);
			canvas.DrawPath(ForecastStormAreaCache, StormPaint);
		}
		finally
		{
			canvas.Restore();
		}
	}

	// thanks! @soshi1822
	public static (Location s, Location e)[]? GetSharedCircumscribedCrossPoints(TyphoonCircle c1, TyphoonCircle c2)
	{
		// 1つ目の円の半径が小さいようにする
		if (c1.RangeKilometer > c2.RangeKilometer)
			(c1, c2) = (c2, c1);

		var d = c1.RawCenter.DistanceTo(c2.RawCenter);
		var s = c1.RawCenter.InitialBearingTo(c2.RawCenter);

		var rd = c2.RangeKilometer - c1.RangeKilometer;
		var b = Math.Sqrt(Math.Abs(d * d - rd * rd));

		var c = Math.Acos(b / d) * (180 / Math.PI) + 90;

		// 円の中に入っている
		if (double.IsNaN(c))
			return null;

		var k1 = c + s;
		var k2 = s - c;
		if (k1 > 360)
			k1 -= 360;
		if (k2 < 0)
			k2 += 360;

		return new[] {
				(c1.RawCenter.MoveTo(k1, c1.RangeKilometer * 1000), c2.RawCenter.MoveTo(k1, c2.RangeKilometer * 1000)),
				(c1.RawCenter.MoveTo(k2, c1.RangeKilometer * 1000), c2.RawCenter.MoveTo(k2, c2.RangeKilometer * 1000))
			};
	}

	private bool IsDisposed { get; set; }

	public void Dispose()
	{
		IsDisposed = true;

		ForecastLineCache?.Dispose();
		ForecastLineCache = null;

		ForecastStormAreaCache?.Dispose();
		ForecastStormAreaCache = null;

		ForecastCirclesCache?.Dispose();
		ForecastCirclesCache = null;

		GC.SuppressFinalize(this);
	}
}
