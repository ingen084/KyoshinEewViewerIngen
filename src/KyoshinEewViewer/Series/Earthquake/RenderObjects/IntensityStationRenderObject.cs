using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Map;
using KyoshinMonitorLib;
using SkiaSharp;

namespace KyoshinEewViewer.Series.Earthquake.RenderObjects;

public class IntensityStationRenderObject : IRenderObject
{
	public IntensityStationRenderObject(LandLayerType? layerType, string name, Location location, JmaIntensity intensity, bool isRegion, bool isForceVisible = false)
	{
		Name = name;
		Location = location;
		Intensity = intensity;
		IsRegion = isRegion;
		IsForceVisible = isForceVisible;
		LayerType = layerType;
	}

	public string Name { get; set; }
	public Location Location { get; set; }
	public JmaIntensity Intensity { get; set; }
	public bool IsRegion { get; }
	public bool IsForceVisible { get; }
	public LandLayerType? LayerType { get; }

	private SKPaint? textPaint;

	public void Render(SKCanvas canvas, RectD viewRect, double zoom, PointD leftTopPixel, bool isAnimating, bool isDarkTheme)
	{
		if (!IsForceVisible && LayerType == LandLayerType.EarthquakeInformationSubdivisionArea && zoom > 8)
			return;
		if (!IsForceVisible && LayerType == LandLayerType.MunicipalityEarthquakeTsunamiArea && zoom <= 8)
			return;

		var circleSize = zoom * 0.95;
		var circleVector = new PointD(circleSize, circleSize);
		var pointCenter = Location.ToPixel(zoom);
		if (!viewRect.IntersectsWith(new RectD(pointCenter - circleVector, pointCenter + circleVector)))
			return;

		if (!isAnimating && ((LayerType == null && zoom >= 8) ||
			(LayerType == LandLayerType.MunicipalityEarthquakeTsunamiArea && zoom >= 10) ||
			(LayerType == LandLayerType.EarthquakeInformationSubdivisionArea && zoom >= 7.5)))
		{
			// 観測点情報文字の描画
			if (textPaint == null)
				textPaint = new SKPaint
				{
					Style = SKPaintStyle.Fill,
					IsAntialias = true,
					Typeface = FixedObjectRenderer.MainTypeface,
					TextSize = 14,
					Color = isDarkTheme ? SKColors.White : SKColors.Black,
					StrokeWidth = 2,
				};
			var point = (pointCenter - leftTopPixel + new PointD(circleSize * 1.2, circleSize * .5)).AsSKPoint();
			//textPaint.TextSize = (float)Math.Max(circleSize * 1.5, 14);

			textPaint.Style = SKPaintStyle.Stroke;
			textPaint.Color = !isDarkTheme ? SKColors.White : SKColors.Black;
			canvas.DrawText(Name, point, textPaint);
			textPaint.Style = SKPaintStyle.Fill;
			textPaint.Color = isDarkTheme ? SKColors.White : SKColors.Black;
			canvas.DrawText(Name, point, textPaint);
		}

		// 震度アイコンの描画
		FixedObjectRenderer.DrawIntensity(canvas, Intensity, (SKPoint)(pointCenter - leftTopPixel), (float)(circleSize * 2), true, !IsRegion, false);
	}
}
