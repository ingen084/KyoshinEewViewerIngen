using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Projections;
using KyoshinMonitorLib;
using SkiaSharp;
using System;

namespace KyoshinEewViewer.Series.Lightning;

public class LightningRealtimeRenderObject : RealtimeRenderObject
{
	// 音の秒速
	private const double MachPerSecond = 1225000 / 60 / 60;
	private static readonly SKPaint BorderPen = new()
	{
		Style = SKPaintStyle.Stroke,
		StrokeWidth = 1,
		Color = new SKColor(255, 255, 255, 120),
		IsAntialias = true,
	};
	private static readonly SKPaint CenterPen = new()
	{
		Style = SKPaintStyle.Stroke,
		StrokeWidth = 1,
		Color = new SKColor(255, 0, 0, 255),
	};
	private static PointD MarkerSize = new(5, 5);

	private DateTime OccuranceTime { get; }
	private Location Location { get; }

	private double Distance { get; set; }
	private bool NeedUpdate { get; set; }

	public LightningRealtimeRenderObject(DateTime occuranceTime, DateTime nowDateTime, Location location)
	{
		OccuranceTime = occuranceTime;
		BaseTime = nowDateTime;
		Location = location ?? throw new ArgumentNullException(nameof(location));
		NeedUpdate = true;
	}

	private SKPath? cache;
	private double cachedZoom;

	public override void Render(SKCanvas canvas, RectD viewRect, double zoom, PointD leftTopPixel, bool isAnimating, bool isDarkTheme, MapProjection projection)
	{
		var pointCenter = Location.ToPixel(projection, zoom);
		if (!viewRect.IntersectsWith(new RectD(pointCenter - MarkerSize, pointCenter + MarkerSize)))
			return;

		var basePoint = Location.ToPixel(projection, zoom) - leftTopPixel;
		canvas.DrawLine((basePoint - new PointD(5, 5)).AsSKPoint(), (basePoint + new PointD(5, 5)).AsSKPoint(), CenterPen);
		canvas.DrawLine((basePoint - new PointD(-5, 5)).AsSKPoint(), (basePoint + new PointD(-5, 5)).AsSKPoint(), CenterPen);

		if (TimeOffset.TotalSeconds <= .5)
			canvas.DrawCircle(basePoint.AsSKPoint(), (float)(1 - TimeOffset.TotalSeconds) * 20, CenterPen);

		if (zoom <= 6)
			return;

		if (cache == null || NeedUpdate || cachedZoom != zoom)
		{
			cache = PathGenerator.MakeCirclePath(projection, Location, Distance, zoom, (int)(zoom - 5) * 30);
			NeedUpdate = false;
			cachedZoom = zoom;
		}

		if (cache == null)
			return;
		canvas.Save();
		try
		{
			// 発生からの秒数に合わせて縁の太さを変える
			var secs = (BaseTime + TimeOffset - OccuranceTime).TotalSeconds;
			BorderPen.StrokeWidth = (float)Math.Max(1, secs / 7);

			canvas.Translate((float)-leftTopPixel.X, (float)-leftTopPixel.Y);
			canvas.DrawPath(cache, BorderPen);
		}
		finally
		{
			canvas.Restore();
		}
	}
	protected override void OnTick()
	{
		var secs = (BaseTime + TimeOffset - OccuranceTime).TotalSeconds;
		Distance = secs * MachPerSecond;
		NeedUpdate = true;
	}
}
