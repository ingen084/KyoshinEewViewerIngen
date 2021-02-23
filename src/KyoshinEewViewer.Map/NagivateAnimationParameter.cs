using System;

namespace KyoshinEewViewer.Map
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
		public NagivateAnimationParameter(double baseZoom, RectD baseRect, RectD toRect)
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

		private static double CalcTheta(PointD point)
			=> Math.Atan2(point.Y, point.X);

		//public bool RelativeMode { get; }
		public PointD BaseDisplayPoint { get; }
		public double BaseZoom { get; }
		//public double ToZoom { get; }
		public RectD BaseRect { get; }
		public RectD ToRect { get; }

		public double TopLeftTheta { get; }
		public double BottomRightTheta { get; }

		public double TopLeftLength { get; }
		public double BottomRightLength { get; }
	}
}
