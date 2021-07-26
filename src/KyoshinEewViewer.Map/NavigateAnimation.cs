using Avalonia.Animation.Easings;
using KyoshinEewViewer.Map.Projections;
using KyoshinMonitorLib;
using System;
using System.Diagnostics;

namespace KyoshinEewViewer.Map
{
	internal class NavigateAnimation
	{
		//public NagivateAnimationParameter(double baseZoom, double toZoom, Point baseDisplayPoint)
		//{
		//	RelativeMode = true;
		//	BaseDisplayPoint = baseDisplayPoint;
		//	BaseZoom = baseZoom;
		//	ToZoom = toZoom;
		//}
		public NavigateAnimation(double baseZoom, RectD baseRect, RectD toRect, TimeSpan duration)
		{
			BaseZoom = baseZoom;
			BaseRect = baseRect;
			ToRect = toRect;
			Duration = duration;
			Stopwatch = new Stopwatch();

			var topLeftDiff = toRect.TopLeft - baseRect.TopLeft;
			var bottomRightDiff = toRect.BottomRight - baseRect.BottomRight;
			TopLeftTheta = CalcTheta(topLeftDiff);
			BottomRightTheta = CalcTheta(bottomRightDiff);

			TopLeftLength = Math.Sqrt(Math.Pow(topLeftDiff.X, 2) + Math.Pow(topLeftDiff.Y, 2));
			BottomRightLength = Math.Sqrt(Math.Pow(bottomRightDiff.X, 2) + Math.Pow(bottomRightDiff.Y, 2));
		}

		private static double CalcTheta(PointD point)
			=> Math.Atan2(point.Y, point.X);

		public void Start()
			=> Stopwatch.Start();

		public double CurrentProgress
		{
			get
			{
				if (Duration.Ticks <= 0)
					return 1;
				if (!Stopwatch.IsRunning)
					return 0;
				return Math.Min(1, Stopwatch.ElapsedMilliseconds / Duration.TotalMilliseconds);
			}
		}
		public (double zoom, Location centerLocation) GetCurrentParameter(MapProjection proj, double currentZoom, RectD paddedRect)
		{
			var progress = CurrentProgress;
			if (Easing != null)
				progress = Easing.Ease(progress);
			var rawBoundPixel = new RectD(
				new PointD(
					BaseRect.Left + Math.Cos(TopLeftTheta) * (TopLeftLength * progress),
					BaseRect.Top + Math.Sin(TopLeftTheta) * (TopLeftLength * progress)),
				new PointD(
					BaseRect.Left + BaseRect.Width + Math.Cos(BottomRightTheta) * (BottomRightLength * progress),
					BaseRect.Top + BaseRect.Height + Math.Sin(BottomRightTheta) * (BottomRightLength * progress)));

			var boundPixel = new RectD(
				rawBoundPixel.TopLeft.ToLocation(proj, BaseZoom).ToPixel(proj, currentZoom),
				rawBoundPixel.BottomRight.ToLocation(proj, BaseZoom).ToPixel(proj, currentZoom));

			var relativeZoom = Math.Log(Math.Min(paddedRect.Width / boundPixel.Width, paddedRect.Height / boundPixel.Height), 2);
			return (currentZoom + relativeZoom,
				new PointD(
					boundPixel.Left + boundPixel.Width / 2,
					boundPixel.Top + boundPixel.Height / 2).ToLocation(proj, currentZoom));
		}

		public bool IsRunning => Stopwatch.IsRunning && Stopwatch.Elapsed < Duration;

		public Easing Easing { get; set; } = new CubicEaseOut();
		private Stopwatch Stopwatch { get; }
		public TimeSpan Duration { get; }
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
