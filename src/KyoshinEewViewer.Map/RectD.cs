using System;

namespace KyoshinEewViewer.Map
{
	public struct RectD
	{
		public RectD(double x, double y, double width, double height)
		{
			if (width < 0 || height < 0)
				throw new ArgumentException();

			X = x;
			Y = y;
			Width = width;
			Height = height;
		}
		public RectD(PointD point1, PointD point2)
		{
			X = Math.Min(point1.X, point2.X);
			Y = Math.Min(point1.Y, point2.Y);

			Width = Math.Max(point1.X, point2.X) - X;
			Height = Math.Max(point1.Y, point2.Y) - Y;
		}

		public double X { get; set; }
		public double Y { get; set; }

		public double Width { get; set; }
		public double Height { get; set; }

		public double Left => X;
		public double Top => Y;
		public double Right => X + Width;
		public double Bottom => Y + Height;

		public PointD TopLeft => new PointD(Left, Top);
		public PointD TopRight => new PointD(Right, Top);
		public PointD BottomLeft => new PointD(Left, Bottom);
		public PointD BottomRight => new PointD(Right, Bottom);

		public bool IntersectsWith(RectD rect)
			 => (rect.Left <= Right) &&
				(rect.Right >= Left) &&
				(rect.Top <= Bottom) &&
				(rect.Bottom >= Top);

		public override string ToString() => $"{X},{Y},{Width},{Height}";
	}
}
