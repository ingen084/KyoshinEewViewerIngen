using KyoshinEewViewer.CustomControls;
using KyoshinEewViewer.MapControl;
using KyoshinEewViewer.MapControl.Projections;
using KyoshinMonitorLib;
using System;
using System.Windows;
using System.Windows.Media;

namespace EarthquakeRenderTest.RenderObjects
{
	public class IntensityStationRenderObject : IRenderObject
	{
		public Location Location { get; set; }
		public JmaIntensity Intensity { get; set; }
		public void Render(DrawingContext context, Rect viewRect, double zoom, Point leftTopPixel, bool isDarkTheme, MapProjection projection)
		{
			var circleSize = (zoom - 2) * 1.75;
			var circleVector = new Vector(circleSize, circleSize);
			var pointCenter = Location.ToPixel(projection, zoom);
			if (!viewRect.IntersectsWith(new Rect(pointCenter - circleVector, pointCenter + circleVector)))
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
			FixedObjectRenderer.DrawIntensity(context, Intensity, pointCenter - (Vector)leftTopPixel, circleSize * 2, true, true);
		}
	}
}
