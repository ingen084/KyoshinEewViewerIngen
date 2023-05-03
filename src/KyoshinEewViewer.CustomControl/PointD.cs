using SkiaSharp;

namespace KyoshinEewViewer.CustomControl;

internal struct PointD
{
	public PointD(double x = 0, double y = 0)
	{
		X = x;
		Y = y;
	}

	public double X { get; set; }
	public double Y { get; set; }

	public static PointD operator +(PointD p1, PointD p2)
		=> new(p1.X + p2.X, p1.Y + p2.Y);
	public static PointD operator +(SKPoint p1, PointD p2)
		=> new(p1.X + p2.X, p1.Y + p2.Y);
	public static PointD operator -(PointD p1, PointD p2)
		=> new(p1.X - p2.X, p1.Y - p2.Y);
	public static PointD operator -(SKPoint p1, PointD p2)
		=> new(p1.X - p2.X, p1.Y - p2.Y);

	public static explicit operator SKPoint(PointD s)
		=> new((float)s.X, (float)s.Y);
	public static explicit operator PointD(SKPoint s)
		=> new(s.X, s.Y);

	public SKPoint AsSkPoint()
		=> new((float)X, (float)Y);

	public override string ToString() => $"{{{X},{Y}}}";
}
