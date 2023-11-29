using SkiaSharp;
using System;
using System.Buffers;

namespace KyoshinEewViewer.Map.Simplify;

// Reference: https://www.codeproject.com/Articles/18936/A-C-Implementation-of-Douglas-Peucker-Line-Appro

public class DouglasPeucker
{
	/// <summary>
	/// Uses the Douglas Peucker algorithm to reduce the number of points.
	/// </summary>
	/// <param name="points">The points.</param>
	/// <param name="tolerance">The tolerance.</param>
	/// <returns></returns>
	public static SKPoint[] Reduction(ReadOnlySpan<PointD> points, double tolerance, bool closed)
	{
		if (points.Length < 3)
		{
			var returnPoints2 = new SKPoint[points.Length];
			for (var i = 0; i < returnPoints2.Length; i++)
				returnPoints2[i] = points[i].AsSkPoint();
			return returnPoints2;
		}

		var firstPoint = 0;
		var lastPoint = points.Length - 1;
		var pointIndexesToKeep = ArrayPool<int>.Shared.Rent(points.Length);
		try
		{
			var pointIndexedToKeepIndex = 0;
			//Add the first and last index to the keepers
			pointIndexesToKeep[pointIndexedToKeepIndex++] = firstPoint;
			pointIndexesToKeep[pointIndexedToKeepIndex++] = lastPoint;

			//The first and the last point cannot be the same
			if (closed)
				lastPoint--;

			DouglasPeuckerReduction(points, firstPoint, lastPoint, tolerance, pointIndexesToKeep, ref pointIndexedToKeepIndex);
			Array.Sort(pointIndexesToKeep, 0, pointIndexedToKeepIndex);

			var returnPoints = new SKPoint[pointIndexedToKeepIndex];
			for (var i = 0; i < returnPoints.Length; i++)
				returnPoints[i] = points[pointIndexesToKeep[i]].AsSkPoint();

			return returnPoints;
		}
		finally
		{
			ArrayPool<int>.Shared.Return(pointIndexesToKeep);
		}
	}

	/// <summary>
	/// Douglases the peucker reduction.
	/// </summary>
	/// <param name="points">The points.</param>
	/// <param name="firstPoint">The first point.</param>
	/// <param name="lastPoint">The last point.</param>
	/// <param name="tolerance">The tolerance.</param>
	/// <param name="pointIndexesToKeep">The point index to keep.</param>
	private static void DouglasPeuckerReduction(ReadOnlySpan<PointD> points, int firstPoint, int lastPoint, double tolerance, int[] pointIndexesToKeep, ref int pointIndexedToKeepIndex)
	{
		double maxDistance = 0;
		var indexFarthest = 0;

		for (var index = firstPoint; index < lastPoint; index++)
		{
			var distance = PerpendicularDistance(points[firstPoint], points[lastPoint], points[index]);
			if (distance > maxDistance)
			{
				maxDistance = distance;
				indexFarthest = index;
			}
		}

		if (maxDistance > tolerance && indexFarthest != 0)
		{
			//Add the largest point that exceeds the tolerance
			pointIndexesToKeep[pointIndexedToKeepIndex++] = indexFarthest;

			DouglasPeuckerReduction(points, firstPoint, indexFarthest, tolerance, pointIndexesToKeep, ref pointIndexedToKeepIndex);
			DouglasPeuckerReduction(points, indexFarthest, lastPoint, tolerance, pointIndexesToKeep, ref pointIndexedToKeepIndex);
		}
	}

	/// <summary>
	/// The distance of a point from a line made from point1 and point2.
	/// </summary>
	/// <param name="pt1">The PT1.</param>
	/// <param name="pt2">The PT2.</param>
	/// <param name="p">The p.</param>
	/// <returns></returns>
	public static double PerpendicularDistance(PointD point1, PointD point2, PointD point)
	{
		//Area = |(1/2)(x1y2 + x2y3 + x3y1 - x2y1 - x3y2 - x1y3)|   *Area of triangle
		//Base = v((x1-x2)²+(x1-x2)²)                               *Base of Triangle*
		//Area = .5*Base*H                                          *Solve for height
		//Height = Area/.5/Base

		var area = Math.Abs(.5 * (point1.X * point2.Y + point2.X * point.Y + point.X * point1.Y - point2.X * point1.Y - point.X * point2.Y - point1.X * point.Y));
		var x = point1.X - point2.X;
		var y = point1.Y - point2.Y;

		var bottom = Math.Sqrt(x * x + y * y);

		return area / bottom * 2;
	}
}
