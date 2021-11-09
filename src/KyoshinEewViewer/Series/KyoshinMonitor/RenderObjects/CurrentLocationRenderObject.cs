using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Projections;
using KyoshinMonitorLib;
using SkiaSharp;

namespace KyoshinEewViewer.Series.KyoshinMonitor.RenderObjects;

public class CurrentLocationRenderObject : IRenderObject
{
	//private SKPaint Pen { get; } = new SKPaint
	//	{
	//		Style = SKPaintStyle.Fill,
	//		Color = SKColors.RoyalBlue,
	//		StrokeWidth = 3,
	//		IsAntialias = true,
	//	};
	//private SKPaint Pen2 { get; } = new SKPaint
	//	{
	//		Style = SKPaintStyle.Stroke,
	//		Color = SKColors.AliceBlue,
	//		StrokeWidth = 2,
	//		IsAntialias = true,
	//	};
	private SKPaint Pen { get; } = new SKPaint
	{
		Style = SKPaintStyle.Fill,
		Color = SKColors.Magenta,
		StrokeWidth = 2,
		IsAntialias = true,
	};

	public Location? Location { get; set; }

	public CurrentLocationRenderObject(Location location)
	{
		Location = location;
	}

	public void Render(SKCanvas canvas, RectD viewRect, double zoom, PointD leftTopPixel, bool isAnimating, bool isDarkTheme, MapProjection projection)
	{
		if (Location == null)
			return;

		var size = 5;
		//var minSize = 5;
		//var maxSize = minSize + 1;

		var basePoint = Location.ToPixel(projection, zoom) - leftTopPixel;

		//canvas.DrawCircle(basePoint.AsSKPoint(), (float)maxSize, Pen);
		//canvas.DrawCircle(basePoint.AsSKPoint(), (float)minSize, Pen2);

		canvas.DrawLine((basePoint - new PointD(0, size)).AsSKPoint(), (basePoint + new PointD(0, size)).AsSKPoint(), Pen);
		canvas.DrawLine((basePoint - new PointD(size, 0)).AsSKPoint(), (basePoint + new PointD(size, 0)).AsSKPoint(), Pen);
	}
}
