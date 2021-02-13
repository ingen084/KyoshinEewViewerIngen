using KyoshinEewViewer.MapControl;
using KyoshinEewViewer.MapControl.Projections;
using KyoshinMonitorLib;
using System.Windows;
using System.Windows.Media;

namespace EarthquakeRenderTest.RenderObjects
{
	public class HypoCenterRenderObject : IRenderObject
	{
		public Location Location { get; set; }
		public bool IsLarge { get; }

		public HypoCenterRenderObject(Location location, bool large)
		{
			Location = location;
			IsLarge = large;

			Pen2 =new Pen(new SolidColorBrush(Color.FromArgb(255, 255, 0, 0)), large ? 6 : 3);
			Pen2.Freeze();
			if (!large)
				return;
			Pen = new Pen(new SolidColorBrush(Color.FromArgb(255, 255, 255, 0)), 8);
			Pen.Freeze();
		}

		private Pen Pen { get; }

		private Pen Pen2 { get; }

		public void Render(DrawingContext context, Rect bound, double zoom, Point leftTopPixel, bool isDarkTheme, MapProjection projection)
		{
			var minSize = (IsLarge ? 10 : 2) + (zoom - 5) * 1.25;
			var maxSize = minSize + 1;

			var basePoint = (Point)(Location.ToPixel(projection, zoom) - leftTopPixel);
			if (IsLarge)
			{
				context.DrawLine(Pen, basePoint - new Vector(maxSize, maxSize), basePoint + new Vector(maxSize, maxSize));
				context.DrawLine(Pen, basePoint - new Vector(-maxSize, maxSize), basePoint + new Vector(-maxSize, maxSize));
			}
			context.DrawLine(Pen2, basePoint - new Vector(minSize, minSize), basePoint + new Vector(minSize, minSize));
			context.DrawLine(Pen2, basePoint - new Vector(-minSize, minSize), basePoint + new Vector(-minSize, minSize));
		}
	}
}
