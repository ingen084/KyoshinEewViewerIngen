using KyoshinEewViewer.CustomControl;
using KyoshinMonitorLib;
using SkiaSharp;
using System;

namespace KyoshinEewViewer.Map.Layers;

public class GridLayer : MapLayer
{
	private static readonly SKPaint GridPaint = new()
	{
		Style = SKPaintStyle.Stroke,
		IsAntialias = true,
		StrokeWidth = 1,
		TextSize = 12,
		Typeface = FixedObjectRenderer.MainTypeface,
		Color = new SKColor(100, 100, 100, 100),
	};

	private const float LatInterval = 5;
	private const float LngInterval = 5;

	public override bool NeedPersistentUpdate => false;

	public override void RefreshResourceCache(Avalonia.Controls.Control targetControl) { }

	public override void Render(SKCanvas canvas, LayerRenderParameter param, bool isAnimating)
	{
		{
			var origin = param.ViewAreaRect.Left - (param.ViewAreaRect.Left % LatInterval);
			var count = (int)Math.Ceiling(param.ViewAreaRect.Width / LatInterval) + 1;

			for (var i = 0; i < count; i++)
			{
				var lat = origin + LatInterval * i;
				if (Math.Abs(lat) > 90)
					continue;
				var pix = new Location((float)lat, 0).ToPixel(param.Zoom);
				var h = pix.Y - param.LeftTopPixel.Y;
				canvas.DrawLine(new SKPoint(0, (float)h), new SKPoint((float)param.PixelBound.Width, (float)h), GridPaint);
				canvas.DrawText(lat.ToString(), new SKPoint((float)param.Padding.Left, (float)h), GridPaint);
			}
		}
		{
			var origin = param.ViewAreaRect.Top - (param.ViewAreaRect.Top % LngInterval);
			var count = (int)Math.Ceiling(param.ViewAreaRect.Height / LngInterval) + 1;

			for (var i = 0; i < count; i++)
			{
				var lng = origin + LngInterval * i;
				var pix = new Location(0, (float)lng).ToPixel(param.Zoom);
				var w = pix.X - param.LeftTopPixel.X;
				canvas.DrawLine(new SKPoint((float)w, 0), new SKPoint((float)w, (float)param.PixelBound.Height), GridPaint);
				if (lng > 180)
					lng -= 360;
				if (lng < -180)
					lng += 360;
				canvas.DrawText(lng.ToString(), new SKPoint((float)w, (float)(param.PixelBound.Height - GridPaint.TextSize - param.Padding.Bottom)), GridPaint);
			}
		}
	}
}
