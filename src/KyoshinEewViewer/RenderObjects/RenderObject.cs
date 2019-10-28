using KyoshinMonitorLib;
using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace KyoshinEewViewer.RenderObjects
{
	public abstract class RenderObject
	{
		protected Dispatcher Dispatcher { get; }

		public RenderObject(Dispatcher mainDispatcher)
		{
			Dispatcher = mainDispatcher;
		}

		public abstract void Render(DrawingContext context);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected Point LocationToPoint(Location loc)
			=> (Point)(LatlngToPixel(loc) - LeftTopPoint);

		//MEMO 汎用性のない地図オフセット
		private static Point LeftTopPoint = LatlngToPixel(new Location(47f, 121f)) - new Vector(2, 2);

		// 256
		private const int POW_2_8 = 2 * 2 * 2 * 2 * 2 * 2 * 2 * 2;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static Point LatlngToPixel(Location loc) => new Point(
			128.0 / Math.PI * ((loc.Longitude * Math.PI / 180.0) + Math.PI) * POW_2_8 * 0.1,
			(-(128.0 / Math.PI) / 2.0 * Math.Log((1.0 + Math.Sin(loc.Latitude * Math.PI / 180.0)) / (1.0 - Math.Sin(loc.Latitude * Math.PI / 180.0))) + 128.0) * POW_2_8 * 0.1);
	}
}