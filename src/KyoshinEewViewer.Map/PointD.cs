using MessagePack;
using SkiaSharp;

namespace KyoshinEewViewer.Map
{
	[MessagePackObject]
	public struct PointD
	{
		public PointD(double x = 0, double y = 0)
		{
			X = x;
			Y = y;
		}

		[Key(0)]
		public double X { get; }
		[Key(1)]
		public double Y { get; }

		public static PointD operator +(PointD p1, PointD p2)
			=> new PointD(p1.X + p2.X, p1.Y + p2.Y);

		public static PointD operator -(PointD p1, PointD p2)
			=> new PointD(p1.X - p2.X, p1.Y - p2.Y);

		public SKPoint AsSKPoint()
			=> new SKPoint((float)X, (float)Y);
	}
}
