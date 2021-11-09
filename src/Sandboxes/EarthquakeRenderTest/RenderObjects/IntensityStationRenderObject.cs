using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Projections;
using KyoshinMonitorLib;
using SkiaSharp;

namespace EarthquakeRenderTest.RenderObjects;

public class IntensityStationRenderObject : IRenderObject
{
	public IntensityStationRenderObject(Location location, JmaIntensity intensity)
	{
		Location = location;
		Intensity = intensity;
	}

	public Location Location { get; set; }
	public JmaIntensity Intensity { get; set; }

	public void Render(SKCanvas canvas, RectD viewRect, double zoom, PointD leftTopPixel, bool isDarkTheme, MapProjection projection)
	{
		var circleSize = (zoom - 2) * 1.75;
		var circleVector = new PointD(circleSize, circleSize);
		var pointCenter = Location.ToPixel(projection, zoom);
		if (!viewRect.IntersectsWith(new RectD(pointCenter - circleVector, pointCenter + circleVector)))
			return;

		// 観測点情報文字の描画
		//if (zoom >= Config.ShowNameZoomLevel || zoom >= Config.ShowValueZoomLevel)
		//{
		//	var multiLine = zoom >= Config.ShowNameZoomLevel && zoom >= Config.ShowValueZoomLevel;
		//	var text = new FormattedText(
		//		(zoom >= Config.ShowNameZoomLevel ? Name : "") +
		//		(multiLine ? "\n" : "") +
		//		(zoom >= Config.ShowValueZoomLevel ? (float.IsNaN(intensity) ? "-" : intensity.ToString("0.0")) : ""),
		//		CultureInfo.CurrentCulture,
		//		FlowDirection.LeftToRight,
		//		TypeFace,
		//		14,
		//		isDarkTheme ? Brushes.White : Brushes.Black,
		//		96)
		//	{
		//		LineHeight = 14
		//	};
		//	context.DrawText(text, pointCenter - (Vector)leftTopPixel + new Vector(circleSize, multiLine ? -11 : -4));
		//}

		// 震度アイコンの描画
		FixedObjectRenderer.DrawIntensity(canvas, Intensity, pointCenter - leftTopPixel, (float)(circleSize * 2), true, true);
	}
}
