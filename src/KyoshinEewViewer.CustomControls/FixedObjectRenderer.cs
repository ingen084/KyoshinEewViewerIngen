using KyoshinMonitorLib;
using KyoshinMonitorLib.Images;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace KyoshinEewViewer.CustomControls
{
	public static class FixedObjectRenderer
	{
		static readonly Typeface face = new Typeface(new FontFamily(new Uri("pack://application:,,,/"), "./Resources/#Gen Shin Gothic P Medium"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
		static readonly Typeface face2 = new Typeface(new FontFamily(new Uri("pack://application:,,,/"), "./Resources/#Gen Shin Gothic P Bold"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
		static readonly Typeface intensityFace = new Typeface(new FontFamily(new Uri("pack://application:,,,/"), "./Resources/#Gen Shin Gothic P Bold"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

		public const double INTENSITY_WIDE_SCALE = .75;

		/// <summary>
		/// 震度アイコンを描画する
		/// </summary>
		/// <param name="drawingContext">描画先のDrawingContext</param>
		/// <param name="intensity">描画する震度</param>
		/// <param name="point">座標</param>
		/// <param name="size">描画するサイズ ワイドモードの場合縦サイズになる</param>
		/// <param name="centerPosition">指定した座標を中心座標にするか</param>
		/// <param name="circle">縁を円形にするか wideがfalseのときのみ有効</param>
		/// <param name="wide">ワイドモード(強弱漢字表記)にするか</param>
		public static void DrawIntensity(this DrawingContext drawingContext, JmaIntensity intensity, Point point, double size, bool centerPosition = false, bool circle = false, bool wide = false)
		{
			var halfSize = new Vector(size / 2, size / 2);
			if (wide)
				halfSize.X /= INTENSITY_WIDE_SCALE;
			var leftTop = centerPosition ? point - halfSize : point;
			if (circle && !wide)
				drawingContext.DrawEllipse((Brush)Application.Current.FindResource($"{intensity}Background"), null, centerPosition ? point : (point + halfSize), size / 2, size / 2);
			else
				drawingContext.DrawRectangle((Brush)Application.Current.FindResource($"{intensity}Background"), null, new Rect(leftTop, new Size(wide ? size / INTENSITY_WIDE_SCALE : size, size)));

			switch (intensity)
			{
				case JmaIntensity.Int5Lower:
					{
						var brush = (Brush)Application.Current.FindResource($"Int5LowerForeground");
						if (size < 12)
						{
							drawingContext.DrawText(new FormattedText("-", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, intensityFace, size * 1.25, brush, 1), new Point(leftTop.X + size * .25, leftTop.Y - size * .5));
							break;
						}
						drawingContext.DrawText(new FormattedText("5", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, intensityFace, size, brush, 1), new Point(leftTop.X + size * .1, leftTop.Y - size * .25));
						if (wide)
							drawingContext.DrawText(new FormattedText("弱", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, intensityFace, size * .55, brush, 1), new Point(leftTop.X + size * .65, leftTop.Y + size * .2));
						else
							drawingContext.DrawText(new FormattedText("-", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, intensityFace, size * .75, brush, 1), new Point(leftTop.X + size * .6, leftTop.Y - size * .27));
					}
					return;
				case JmaIntensity.Int5Upper:
					{
						var brush = (Brush)Application.Current.FindResource($"Int5UpperForeground");
						if (size < 12)
						{
							drawingContext.DrawText(new FormattedText("+", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, intensityFace, size * 1.25, brush, 1), new Point(leftTop.X + size * .1, leftTop.Y - size * .4));
							break;
						}
						drawingContext.DrawText(new FormattedText("5", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, intensityFace, size, brush, 1), new Point(leftTop.X + size * .1, leftTop.Y - size * .25));
						if (wide)
							drawingContext.DrawText(new FormattedText("強", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, intensityFace, size * .55, brush, 1), new Point(leftTop.X + size * .65, leftTop.Y + size * .2));
						else
							drawingContext.DrawText(new FormattedText("+", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, intensityFace, size * .75, brush, 1), new Point(leftTop.X + size * .5, leftTop.Y - size * .23));
					}
					return;
				case JmaIntensity.Int6Lower:
					{
						var brush = (Brush)Application.Current.FindResource($"Int6LowerForeground");
						if (size < 12)
						{
							drawingContext.DrawText(new FormattedText("-", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, intensityFace, size * 1.25, brush, 1), new Point(leftTop.X + size * .25, leftTop.Y - size * .5));
							break;
						}
						drawingContext.DrawText(new FormattedText("6", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, intensityFace, size, brush, 1), new Point(leftTop.X + size * .1, leftTop.Y - size * .25));
						if (wide)
							drawingContext.DrawText(new FormattedText("弱", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, intensityFace, size * .55, brush, 1), new Point(leftTop.X + size * .65, leftTop.Y + size * .2));
						else
							drawingContext.DrawText(new FormattedText("-", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, intensityFace, size * .75, brush, 1), new Point(leftTop.X + size * .6, leftTop.Y - size * .27));
					}
					return;
				case JmaIntensity.Int6Upper:
					{
						var brush = (Brush)Application.Current.FindResource($"Int6UpperForeground");
						if (size < 12)
						{
							drawingContext.DrawText(new FormattedText("+", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, intensityFace, size * 1.25, brush, 1), new Point(leftTop.X + size * .1, leftTop.Y - size * .4));
							break;
						}
						drawingContext.DrawText(new FormattedText("6", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, intensityFace, size, brush, 1), new Point(leftTop.X + size * .1, leftTop.Y - size * .25));
						if (wide)
							drawingContext.DrawText(new FormattedText("強", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, intensityFace, size * .55, brush, 1), new Point(leftTop.X + size * .65, leftTop.Y + size * .2));
						else
							drawingContext.DrawText(new FormattedText("+", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, intensityFace, size * .75, brush, 1), new Point(leftTop.X + size * .5, leftTop.Y - size * .23));
					}
					return;
				case JmaIntensity.Unknown:
					drawingContext.DrawText(new FormattedText("-", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, intensityFace, size, (Brush)Application.Current.FindResource($"UnknownForeground"), 1), new Point(leftTop.X + size * (wide ? .52 : .32), leftTop.Y - size * .3));
					return;
				case JmaIntensity.Error:
					drawingContext.DrawText(new FormattedText("E", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, intensityFace, size, (Brush)Application.Current.FindResource($"ErrorForeground"), 1), new Point(leftTop.X + size * (wide ? .35 : .2), leftTop.Y - size * .25));
					return;
			}
			if (size >= 12)
				drawingContext.DrawText(new FormattedText(intensity.ToShortString(), CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, intensityFace, size, (Brush)Application.Current.FindResource($"{intensity}Foreground"), 1), new Point(leftTop.X + size * (wide ? .38 : .22), leftTop.Y - size * .25));
		}

		public static void DrawLinkedRealtimeData(this DrawingContext drawingContext, IEnumerable<ImageAnalysisResult> points, double itemHeight, double firstHeight, double maxWidth, double maxHeight, bool useShindoIcon = true)
		{
			if (points == null) return;

			var brush = (Brush)Application.Current.FindResource($"ForegroundColor");
			var subBrush = (Brush)Application.Current.FindResource($"SubForegroundColor");
			int count = 0;
			double verticalOffset = 0;
			foreach (var point in points)
			{
				double horizontalOffset = 0;
				var height = count == 0 ? firstHeight : itemHeight;
				if (useShindoIcon)
				{
					drawingContext.DrawIntensity(point.GetResultToIntensity().ToJmaIntensity(), new Point(0, verticalOffset), height);
					horizontalOffset += height;
				}
				else
				{
					drawingContext.DrawRectangle(new SolidColorBrush(Color.FromArgb(point.Color.A, point.Color.R, point.Color.G, point.Color.B)), null, new Rect(new Point(0, verticalOffset), new Size(height / 5, height)));
					horizontalOffset += height / 5;
				}

				var region = point.ObservationPoint.Region;
				if (region.Contains(' '))
					region = region.Substring(0, region.IndexOf(' '));

				var regionText = new FormattedText(region, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, face, itemHeight * .6, brush, 1);
				drawingContext.DrawText(regionText, new Point(horizontalOffset + height * 0.1, verticalOffset + (height - regionText.Height) / 2));
				horizontalOffset += Math.Max(regionText.Width, maxWidth / 4);

				var valueText = new FormattedText(point.GetResultToIntensity()?.ToString("0.0") ?? "?", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, face, Math.Min(height * .4, itemHeight * .75), subBrush, 1);
				drawingContext.DrawText(valueText, new Point(maxWidth - valueText.Width, verticalOffset + height * .5));

				var nameText = new FormattedText(point.ObservationPoint.Name, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, face2, itemHeight * .75, brush, 1);
				drawingContext.DrawText(nameText, new Point(horizontalOffset + height * 0.2, verticalOffset + (height - nameText.Height) / 2));

				count++;
				verticalOffset += height;
				if (verticalOffset >= maxHeight)
					return;
			}
		}
	}
}
