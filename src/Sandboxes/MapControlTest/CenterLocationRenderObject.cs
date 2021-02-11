using KyoshinEewViewer.MapControl;
using KyoshinEewViewer.MapControl.Projections;
using KyoshinMonitorLib;
using System.Windows;
using System.Windows.Media;

namespace MapControlTest
{
	public class CenterLocationRenderObject : IRenderObject
	{
		private Brush PointBrush { get; } = new SolidColorBrush(Color.FromArgb(150, 255, 0, 0));

		public Location Location { get; set; }
		public void Render(DrawingContext context, Rect viewRect, double zoom, Point leftTopPixel, bool isDarkTheme, MapProjection projection)
		{
			var circleSize = (zoom - 2) * 2;
			var circleVector = new Vector(circleSize, circleSize);
			var pointCenter = Location.ToPixel(projection, zoom);
			if (!viewRect.IntersectsWith(new Rect(pointCenter - circleVector, pointCenter + circleVector)))
				return;

			var displayCenter = pointCenter - (Vector)leftTopPixel;
			context.DrawRectangle(PointBrush, null, new Rect(displayCenter - circleVector, displayCenter + circleVector));
		}
	}
}
