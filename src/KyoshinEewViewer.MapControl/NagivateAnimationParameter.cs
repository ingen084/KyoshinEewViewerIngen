using System;
using System.Windows;

namespace KyoshinEewViewer.MapControl
{
	internal class NagivateAnimationParameter
	{
		//public NagivateAnimationParameter(double baseZoom, double toZoom, Point baseDisplayPoint)
		//{
		//	RelativeMode = true;
		//	BaseDisplayPoint = baseDisplayPoint;
		//	BaseZoom = baseZoom;
		//	ToZoom = toZoom;
		//}
		public NagivateAnimationParameter(double baseZoom, Rect baseRect, Rect toRect)
		{
			BaseZoom = baseZoom;
			BaseRect = baseRect;
			ToRect = toRect;

			var topLeftDiff = toRect.TopLeft - baseRect.TopLeft;
			var bottomRightDiff = toRect.BottomRight - baseRect.BottomRight;
			TopLeftTheta = CalcTheta(topLeftDiff);
			BottomRightTheta = CalcTheta(bottomRightDiff);

			TopLeftLength = Math.Sqrt(Math.Pow(topLeftDiff.X, 2) + Math.Pow(topLeftDiff.Y, 2));
			BottomRightLength = Math.Sqrt(Math.Pow(bottomRightDiff.X, 2) + Math.Pow(bottomRightDiff.Y, 2));
		}

		private double CalcTheta(Vector point)
			=> Math.Atan2(point.Y, point.X);

		//public bool RelativeMode { get; }
		public Point BaseDisplayPoint { get; }
		public double BaseZoom { get; }
		//public double ToZoom { get; }
		public Rect BaseRect { get; }
		public Rect ToRect { get; }

		public double TopLeftTheta { get; }
		public double BottomRightTheta { get; }

		public double TopLeftLength { get; }
		public double BottomRightLength { get; }
	}
}
