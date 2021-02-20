using KyoshinEewViewer.MapControl;
using KyoshinEewViewer.MapControl.Projections;
using KyoshinMonitorLib;
using System.Windows;
using System.Windows.Media;

namespace KyoshinEewViewer.RenderObjects
{
	public class EewCenterRenderObject : IRenderObject
	{
		public Location Location { get; set; }

		public EewCenterRenderObject(Location location)
		{
			Location = location;
			Pen.Freeze();
			Pen2.Freeze();
		}

		//TODO: EEWのたびにBrush初期化させるのはまずくないか…？
		private Pen Pen { get; } = new Pen(new SolidColorBrush(Color.FromArgb(255, 255, 255, 0)), 4);

		private Pen Pen2 { get; } = new Pen(new SolidColorBrush(Color.FromArgb(150, 255, 0, 0)), 2);

		public void Render(DrawingContext context, Rect bound, double zoom, Point leftTopPixel, bool isDarkTheme, MapProjection projection)
		{
			var minSize = 5 + (zoom - 5) * 1.25;
			var maxSize = minSize + 1;

			var basePoint = (Point)(Location.ToPixel(projection, zoom) - leftTopPixel);
			context.DrawLine(Pen, basePoint - new Vector(maxSize, maxSize), basePoint + new Vector(maxSize, maxSize));
			context.DrawLine(Pen, basePoint - new Vector(-maxSize, maxSize), basePoint + new Vector(-maxSize, maxSize));

			context.DrawLine(Pen2, basePoint - new Vector(minSize, minSize), basePoint + new Vector(minSize, minSize));
			context.DrawLine(Pen2, basePoint - new Vector(-minSize, minSize), basePoint + new Vector(-minSize, minSize));
		}
	}
}