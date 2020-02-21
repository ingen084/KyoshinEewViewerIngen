using KyoshinMonitorLib;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace KyoshinEewViewer.MapControl.RenderObjects
{
	public class EewCenterRenderObject : RenderObject
	{
		public Location Location { get; set; }

		public EewCenterRenderObject(Location location)
		{
			Location = location;
			Pen.Freeze();
			Pen2.Freeze();
		}

		//TODO: EEWのたびにBrush初期化させるのはまずくないか…？
		private Pen Pen { get; } = new Pen(new SolidColorBrush(Color.FromArgb(150, 255, 0, 0)), 4);

		private Pen Pen2 { get; } = new Pen(new SolidColorBrush(Color.FromArgb(200, 255, 255, 0)), 2);

		public override void Render(DrawingContext context, Rect bound, double zoom, Point leftTopPixel)
		{
			var minSize = 5 + (zoom - 5) * 1.25;
			var maxSize = minSize * 1.3;

			var basePoint = (Point)(Location.ToPixel(zoom) - leftTopPixel);
			context.DrawLine(Pen, basePoint - new Vector(maxSize, maxSize), basePoint + new Vector(maxSize, maxSize));
			context.DrawLine(Pen, basePoint - new Vector(-maxSize, maxSize), basePoint + new Vector(-maxSize, maxSize));

			context.DrawLine(Pen2, basePoint - new Vector(minSize, minSize), basePoint + new Vector(minSize, minSize));
			context.DrawLine(Pen2, basePoint - new Vector(-minSize, minSize), basePoint + new Vector(-minSize, minSize));
		}
	}
}