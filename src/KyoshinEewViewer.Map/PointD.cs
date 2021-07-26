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
		public double X { get; set; }
		[Key(1)]
		public double Y { get; set; }

		public static PointD operator +(PointD p1, PointD p2)
			=> new(p1.X + p2.X, p1.Y + p2.Y);

		public static PointD operator -(PointD p1, PointD p2)
			=> new(p1.X - p2.X, p1.Y - p2.Y);

		public static PointD operator /(PointD p1, double d)
			=> new(p1.X + d, p1.Y + d);

		public SKPoint AsSKPoint()
			=> new((float)X, (float)Y);
	}
}
