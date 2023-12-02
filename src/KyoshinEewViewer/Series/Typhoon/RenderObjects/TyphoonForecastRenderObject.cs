using KyoshinEewViewer.Map;
using KyoshinEewViewer.Series.Typhoon.Models;
using KyoshinMonitorLib;
using SkiaSharp;
using System;
using System.Linq;

namespace KyoshinEewViewer.Series.Typhoon.RenderObjects;

public class TyphoonForecastRenderObject(TyphoonPlace currentPlace, TyphoonPlace[] forecastPlaces) : IDisposable
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

	private Location StartLocation { get; } = currentPlace.Center;
	private TyphoonRenderCircle? CurrentStormCircle { get; } = currentPlace.Storm;
	private TyphoonPlace[] ForecastPlaces { get; } = forecastPlaces ?? throw new ArgumentNullException(nameof(forecastPlaces));

	private const int CacheZoom = 5;

	private SKPath? ForecastLineCache { get; set; }
	private SKPath? ForecastStormAreaCache { get; set; }
	private SKPath? ForecastCirclesCache { get; set; }

	public void Render(SKCanvas canvas, double zoom)
	{
		if (IsDisposed)
			return;

		// 実際のズームに合わせるためのスケール
		var scale = Math.Pow(2, zoom - CacheZoom);

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

			(TyphoonRenderCircle? strong, TyphoonRenderCircle? storm) beforeCircle = (new(StartLocation, 0, StartLocation), CurrentStormCircle);
			foreach (var place in ForecastPlaces)
			{
				// 強風域
				if (place.Strong != null && beforeCircle.strong != null &&
					GetSharedCircumscribedCrossPoints(beforeCircle.strong, place.Strong) is { } flines)
				{
					foreach (var (s, e) in flines)
						ForecastLineCache.AddPoly([
							s.ToPixel(CacheZoom).AsSkPoint(),
							e.ToPixel(CacheZoom).AsSkPoint(),
						], false);
				}

				// 暴風域
				if (place.Storm != null)
				{
					using var a = PathGenerator.MakeCirclePath(place.Storm.RawCenter, place.Storm.RangeKilometer * 1000, CacheZoom, 90);
					ForecastStormAreaCache.Op(a, SKPathOp.Union, ForecastStormAreaCache);
				}
				if (place.Storm != null && beforeCircle.storm != null &&
					GetSharedCircumscribedCrossPoints(beforeCircle.storm, place.Storm) is { } slines)
				{
					if (slines.Length != 2)
						continue;
					// 台形のポリゴンを作成して合成
					using var a = new SKPath();
					a.AddPoly([
						slines[0].s.ToPixel(CacheZoom).AsSkPoint(),
						slines[0].e.ToPixel(CacheZoom).AsSkPoint(),
						slines[1].e.ToPixel(CacheZoom).AsSkPoint(),
						slines[1].s.ToPixel(CacheZoom).AsSkPoint(),
					], true);
					ForecastStormAreaCache.Op(a, SKPathOp.Union, ForecastStormAreaCache);
				}

				beforeCircle = (place.Strong, place.Storm);
			}
		}
		canvas.DrawPath(ForecastLineCache, ForecastLinePaint);

		ForecastCirclePaint.StrokeWidth = (float)(2 / scale);
		ForecastCirclePaint.PathEffect = SKPathEffect.CreateDash([(float)(5 / scale), (float)(5 / scale)], 1);
		if (ForecastCirclesCache == null)
		{
			ForecastCirclesCache = new SKPath();
			foreach (var strong in ForecastPlaces.Select(p => p.Strong))
			{
				if (strong == null)
					continue;
				PathGenerator.MakeCirclePath(strong.RawCenter, strong.RangeKilometer * 1000, CacheZoom, 90, ForecastCirclesCache);
			}
		}
		canvas.DrawPath(ForecastCirclesCache, ForecastCirclePaint);

		StormPaint.StrokeWidth = (float)(1 / scale);
		canvas.DrawPath(ForecastStormAreaCache, StormPaint);
	}

	// thanks! @soshi1822
	public static (Location s, Location e)[]? GetSharedCircumscribedCrossPoints(TyphoonRenderCircle c1, TyphoonRenderCircle c2)
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
