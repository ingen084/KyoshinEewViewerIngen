using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace KyoshinEewViewer.Map.Simplify;

public static class Visvalingam
{
	public static SKPoint[] Simplify(PointD[] srcPoints, float minArea)
	{
		if (srcPoints.Length < 3)
			return srcPoints.Select(p => p.AsSkPoint()).ToArray();

		var points = new List<PointD>(srcPoints);

		while (true)
		{
			var smallestArea = double.MaxValue;
			var smallestAreaI = 1;

			for (var i = 1; i < points.Count - 1; i++)
			{
				var nextArea = TriangleArea(points[i - 1], points[i], points[i + 1]);
				if (nextArea < smallestArea)
				{
					smallestArea = nextArea;
					smallestAreaI = i;
				}
			}

			if (smallestArea >= minArea || points.Count <= 3)
				break;

			points.RemoveAt(smallestAreaI);
		}

		return points.Select(p => p.AsSkPoint()).ToArray();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static double TriangleArea(PointD a, PointD b, PointD c)
		=> Math.Abs(
			(
				a.X * (b.Y - c.Y) +
				b.X * (c.Y - a.Y) +
				c.X * (a.Y - b.Y)
			) / 2f
		);
}
