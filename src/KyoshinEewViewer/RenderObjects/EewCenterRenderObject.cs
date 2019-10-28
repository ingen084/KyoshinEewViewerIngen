using KyoshinMonitorLib;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace KyoshinEewViewer.RenderObjects
{
	public class EewCenterRenderObject : RenderObject
	{
		public Location Location { get; set; }

		public EewCenterRenderObject(Dispatcher mainDispatcher, Location location) : base(mainDispatcher)
		{
			Location = location;
			Pen.Freeze();
			Pen2.Freeze();
		}

		//TODO: EEWのたびにBrush初期化させるのはまずくないか…？
		private Pen Pen { get; } = new Pen(new SolidColorBrush(Color.FromArgb(150, 255, 0, 0)), 4);

		private Pen Pen2 { get; } = new Pen(new SolidColorBrush(Color.FromArgb(150, 255, 255, 0)), 2);

		public override void Render(DrawingContext context)
		{
			var basePoint = LocationToPoint(Location);
			context.DrawLine(Pen, basePoint - new Vector(6, 6), basePoint + new Vector(6, 6));
			context.DrawLine(Pen, basePoint - new Vector(-6, 6), basePoint + new Vector(-6, 6));

			context.DrawLine(Pen2, basePoint - new Vector(5, 5), basePoint + new Vector(5, 5));
			context.DrawLine(Pen2, basePoint - new Vector(-5, 5), basePoint + new Vector(-5, 5));
		}
	}
}