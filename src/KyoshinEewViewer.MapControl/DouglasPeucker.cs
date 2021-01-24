using System;
using System.Collections.Generic;
using System.Windows;

namespace KyoshinEewViewer.MapControl
{
	// Reference: https://www.codeproject.com/Articles/18936/A-C-Implementation-of-Douglas-Peucker-Line-Appro

	public class DouglasPeucker
	{
		/// <summary>
		/// Uses the Douglas Peucker algorithm to reduce the number of points.
		/// </summary>
		/// <param name="points">The points.</param>
		/// <param name="tolerance">The tolerance.</param>
		/// <returns></returns>
		public static Point[] Reduction(Point[] points, double tolerance, bool closed)
		{
			if (points == null || points.Length < 3)
				return points;

			int firstPoint = 0;
			int lastPoint = points.Length - 1;
			var pointIndexsToKeep = new List<int>
			{
                //Add the first and last index to the keepers
                firstPoint,
				lastPoint
			};

			//The first and the last point cannot be the same
			if (closed)
				lastPoint--;

			DouglasPeuckerReduction(ref points, ref firstPoint, ref lastPoint, ref tolerance, ref pointIndexsToKeep);
			pointIndexsToKeep.Sort();

			var returnPoints = new Point[pointIndexsToKeep.Count];
			for (int i = 0; i < returnPoints.Length; i++)
				returnPoints[i] = points[pointIndexsToKeep[i]];

			return returnPoints;
		}

		/// <summary>
		/// Douglases the peucker reduction.
		/// </summary>
		/// <param name="points">The points.</param>
		/// <param name="firstPoint">The first point.</param>
		/// <param name="lastPoint">The last point.</param>
		/// <param name="tolerance">The tolerance.</param>
		/// <param name="pointIndexsToKeep">The point index to keep.</param>
		private static void DouglasPeuckerReduction(ref Point[] points, ref int firstPoint, ref int lastPoint, ref double tolerance, ref List<int> pointIndexsToKeep)
		{
			double maxDistance = 0;
			int indexFarthest = 0;

			for (int index = firstPoint; index < lastPoint; index++)
			{
				double distance = PerpendicularDistance(ref points[firstPoint], ref points[lastPoint], ref points[index]);
				if (distance > maxDistance)
				{
					maxDistance = distance;
					indexFarthest = index;
				}
			}

			if (maxDistance > tolerance && indexFarthest != 0)
			{
				//Add the largest point that exceeds the tolerance
				pointIndexsToKeep.Add(indexFarthest);

				DouglasPeuckerReduction(ref points, ref firstPoint, ref indexFarthest, ref tolerance, ref pointIndexsToKeep);
				DouglasPeuckerReduction(ref points, ref indexFarthest, ref lastPoint, ref tolerance, ref pointIndexsToKeep);
			}
		}

		/// <summary>
		/// The distance of a point from a line made from point1 and point2.
		/// </summary>
		/// <param name="pt1">The PT1.</param>
		/// <param name="pt2">The PT2.</param>
		/// <param name="p">The p.</param>
		/// <returns></returns>
		public static double PerpendicularDistance(ref Point point1, ref Point point2, ref Point point)
		{
			//Area = |(1/2)(x1y2 + x2y3 + x3y1 - x2y1 - x3y2 - x1y3)|   *Area of triangle
			//Base = v((x1-x2)²+(x1-x2)²)                               *Base of Triangle*
			//Area = .5*Base*H                                          *Solve for height
			//Height = Area/.5/Base

			double area = Math.Abs(.5 * (point1.X * point2.Y + point2.X * point.Y + point.X * point1.Y - point2.X * point1.Y - point.X * point2.Y - point1.X * point.Y));
			var x = point1.X - point2.X;
			var y = point1.Y - point2.Y;

			double bottom = Math.Sqrt(x * x + y * y);

			return area / bottom * 2;
		}
	}
}
