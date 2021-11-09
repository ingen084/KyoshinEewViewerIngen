using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Map.Projections;
using KyoshinMonitorLib;
using SkiaSharp;
using System;

namespace KyoshinEewViewer.Map.Layers;

internal class GridLayer : MapLayerBase
{
	private static SKPaint? GridPaint;

	private const float LatInterval = 5;
	private const float LngInterval = 5;

	public GridLayer(MapProjection projection) : base(projection) { }

	public override void Render(SKCanvas canvas, bool isAnimating)
	{
		if (GridPaint == null)
			GridPaint = new SKPaint
			{
				Style = SKPaintStyle.Stroke,
				IsAntialias = true,
				StrokeWidth = 1,
				TextSize = 12,
				Typeface = FixedObjectRenderer.MainTypeface,
				Color = new SKColor(100, 100, 100, 100),
			};

		{
			var origin = ViewAreaRect.Left - (ViewAreaRect.Left % LatInterval);
			var count = (int)Math.Ceiling(ViewAreaRect.Width / LatInterval) + 1;

			for (var i = 0; i < count; i++)
			{
				var lat = origin + LatInterval * i;
				if (Math.Abs(lat) > 90)
					continue;
				var pix = new Location((float)lat, 0).ToPixel(Projection, Zoom);
				var h = pix.Y - LeftTopPixel.Y;
				canvas.DrawLine(new SKPoint(0, (float)h), new SKPoint((float)PixelBound.Width, (float)h), GridPaint);
				canvas.DrawText(lat.ToString(), new SKPoint(0, (float)h), GridPaint);
			}
		}
		{
			var origin = ViewAreaRect.Top - (ViewAreaRect.Top % LngInterval);
			var count = (int)Math.Ceiling(ViewAreaRect.Height / LngInterval) + 1;

			for (var i = 0; i < count; i++)
			{
				var lng = origin + LngInterval * i;
				var pix = new Location(0, (float)lng).ToPixel(Projection, Zoom);
				var w = pix.X - LeftTopPixel.X;
				canvas.DrawLine(new SKPoint((float)w, 0), new SKPoint((float)w, (float)PixelBound.Height), GridPaint);
				if (lng > 180)
					lng -= 360;
				if (lng < -180)
					lng += 360;
				canvas.DrawText(lng.ToString(), new SKPoint((float)w, (float)(PixelBound.Height - GridPaint.TextSize)), GridPaint);
			}
		}
	}
}
