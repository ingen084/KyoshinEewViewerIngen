using Avalonia.Animation.Easings;
using KyoshinMonitorLib;
using System;
using System.Diagnostics;

namespace KyoshinEewViewer.Map;

internal class NavigateAnimation
{
	//public NagivateAnimationParameter(double baseZoom, double toZoom, Point baseDisplayPoint)
	//{
	//	RelativeMode = true;
	//	BaseDisplayPoint = baseDisplayPoint;
	//	BaseZoom = baseZoom;
	//	ToZoom = toZoom;
	//}
	public NavigateAnimation(double baseZoom, double minZoom, double maxZoom, RectD baseRect, RectD toRect, TimeSpan duration, RectD paddedRect)
	{
		BaseZoom = baseZoom;
		BaseRect = baseRect;
		ToRect = toRect;
		Duration = duration;
		Stopwatch = new Stopwatch();

		// 最大ズームに補正
		var relativeZoom = Math.Log(Math.Min(paddedRect.Width / toRect.Width, paddedRect.Height / toRect.Height), 2);
		// 最大ズームを超えていた場合補正しつつ見直す
		if ((baseZoom + relativeZoom) > maxZoom)
		{
			var centerPixel = new PointD(
				toRect.Left + toRect.Width / 2,
				toRect.Top + toRect.Height / 2).ToLocation(baseZoom).ToPixel(maxZoom);
			var halfSize = new PointD(paddedRect.Width / 2, paddedRect.Height / 2);

			ToRect = new RectD((centerPixel - halfSize).ToLocation(maxZoom).ToPixel(baseZoom),
				(centerPixel + halfSize).ToLocation(maxZoom).ToPixel(baseZoom));
		}
		// 最小ズームを超えていた場合補正しつつ見直す
		else if ((baseZoom + relativeZoom) < minZoom)
		{
			var centerPixel = new PointD(
				toRect.Left + toRect.Width / 2,
				toRect.Top + toRect.Height / 2).ToLocation(baseZoom).ToPixel(minZoom);
			var halfSize = new PointD(paddedRect.Width / 2, paddedRect.Height / 2);

			ToRect = new RectD((centerPixel - halfSize).ToLocation(minZoom).ToPixel(baseZoom),
				(centerPixel + halfSize).ToLocation(minZoom).ToPixel(baseZoom));
		}

		// theta length の計算
		var topLeftDiff = ToRect.TopLeft - baseRect.TopLeft;
		var bottomRightDiff = ToRect.BottomRight - baseRect.BottomRight;
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
		get {
			if (Duration.Ticks <= 0)
				return 1;
			if (!Stopwatch.IsRunning)
				return 0;
			return Math.Min(1, Stopwatch.ElapsedMilliseconds / Duration.TotalMilliseconds);
		}
	}
	public (double zoom, Location centerLocation) GetCurrentParameter(double currentZoom, RectD paddedRect)
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
			rawBoundPixel.TopLeft.ToLocation(BaseZoom).ToPixel(currentZoom),
			rawBoundPixel.BottomRight.ToLocation(BaseZoom).ToPixel(currentZoom));

		var relativeZoom = Math.Log(Math.Min(paddedRect.Width / boundPixel.Width, paddedRect.Height / boundPixel.Height), 2);
		return (currentZoom + relativeZoom,
			new PointD(
				boundPixel.Left + boundPixel.Width / 2,
				boundPixel.Top + boundPixel.Height / 2).ToLocation(currentZoom));
	}

	public bool IsRunning => Stopwatch.IsRunning && Stopwatch.Elapsed < Duration;

	public Easing Easing { get; set; } = new ExponentialEaseOut();
	private Stopwatch Stopwatch { get; }
	public TimeSpan Duration { get; }
	//public bool RelativeMode { get; }
	//public PointD BaseDisplayPoint { get; }
	public double BaseZoom { get; }
	//public double ToZoom { get; }
	public RectD BaseRect { get; }
	public RectD ToRect { get; }

	public double TopLeftTheta { get; }
	public double BottomRightTheta { get; }

	public double TopLeftLength { get; }
	public double BottomRightLength { get; }
}
