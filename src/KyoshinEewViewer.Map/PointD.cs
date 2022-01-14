using MessagePack;
using SkiaSharp;
using System;

namespace KyoshinEewViewer.Map;

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

	[IgnoreMember]
	public double Length => Math.Sqrt(X * X + Y * Y);
	public PointD Normalize() => this / Length;
	[IgnoreMember]
	public double Direction => DirectionRadian * 180d / Math.PI;
	[IgnoreMember]
	public double DirectionRadian => Math.Atan2(Y, X);
	public static PointD operator *(PointD p1, double d)
		=> new(p1.X * d, p1.Y * d);
	public static PointD operator /(PointD p1, double d)
		=> new(p1.X / d, p1.Y / d);

	public static explicit operator SKPoint(PointD s)
		=> new((float)s.X, (float)s.Y);
	public static explicit operator PointD(SKPoint s)
		=> new(s.X, s.Y);

	public SKPoint AsSKPoint()
		=> new((float)X, (float)Y);

	public override string ToString() => $"{{{X},{Y}}}";
}
