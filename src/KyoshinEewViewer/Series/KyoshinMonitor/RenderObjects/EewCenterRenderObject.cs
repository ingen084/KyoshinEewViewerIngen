using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Projections;
using KyoshinMonitorLib;
using SkiaSharp;

namespace KyoshinEewViewer.Series.KyoshinMonitor.RenderObjects
{
	public class EewCenterRenderObject : IRenderObject
	{
		public Location Location { get; set; }
		public bool IsLarge { get; }

		public EewCenterRenderObject(Location location, bool large)
		{
			Location = location;
			IsLarge = large;

			Pen2 = new SKPaint
			{
				Style = SKPaintStyle.Stroke,
				Color = new SKColor(255, 0, 0, 255),
				StrokeWidth = large ? 4 : 3,
				IsAntialias = true,
			};
			if (!large)
				return;
			Pen = new SKPaint
			{
				Style = SKPaintStyle.Stroke,
				Color = new SKColor(255, 255, 0, 255),
				StrokeWidth = 6,
				IsAntialias = true,
			};
		}

		private SKPaint? Pen { get; }

		private SKPaint Pen2 { get; }

		public void Render(SKCanvas canvas, RectD viewRect, double zoom, PointD leftTopPixel, bool isDarkTheme, MapProjection projection)
		{
			var minSize = (IsLarge ? 8 : 6) + (zoom - 5) * 1.25;
			var maxSize = minSize + 1;

			var basePoint = Location.ToPixel(projection, zoom) - leftTopPixel;
			if (IsLarge)
			{
				canvas.DrawLine((basePoint - new PointD(maxSize, maxSize)).AsSKPoint(), (basePoint + new PointD(maxSize, maxSize)).AsSKPoint(), Pen);
				canvas.DrawLine((basePoint - new PointD(-maxSize, maxSize)).AsSKPoint(), (basePoint + new PointD(-maxSize, maxSize)).AsSKPoint(), Pen);
			}
			canvas.DrawLine((basePoint - new PointD(minSize, minSize)).AsSKPoint(), (basePoint + new PointD(minSize, minSize)).AsSKPoint(), Pen2);
			canvas.DrawLine((basePoint - new PointD(-minSize, minSize)).AsSKPoint(), (basePoint + new PointD(-minSize, minSize)).AsSKPoint(), Pen2);
		}
	}
}
