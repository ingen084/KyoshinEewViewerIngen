using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Projections;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Services;
using KyoshinMonitorLib;
using SkiaSharp;
using System;

namespace KyoshinEewViewer.Series.KyoshinMonitor.RenderObjects
{
	public class RawIntensityRenderObject : IRenderObject
	{
		public RawIntensityRenderObject(Location? location, string? name, float rawIntensity = float.NaN)
		{
			Location = location ?? throw new ArgumentNullException(nameof(location));
			Config = ConfigurationService.Current.RawIntensityObject;
			RawIntensity = rawIntensity;
			Name = name ?? throw new ArgumentNullException(nameof(name));
		}

		private static SKPaint? textPaint;
		private static SKPaint? invalidatePaint;
		private static SKPaint? pointPaint;

		/// <summary>
		/// 表示名
		/// </summary>
		public string Name { get; }
		/// <summary>
		/// 地理座標
		/// </summary>
		public Location Location { get; set; }

		/// <summary>
		/// 生の震度の値
		/// </summary>
		public double RawIntensity { get; set; }

		private KyoshinEewViewerConfiguration.RawIntensityObjectConfig Config { get; }

		private SKColor intensityColor;
		/// <summary>
		/// その地点の色
		/// </summary>
		public SKColor IntensityColor
		{
			get => intensityColor;
			set {
				if (intensityColor == value)
					return;
				intensityColor = value;
			}
		}

		public void Render(SKCanvas canvas, RectD bound, double zoom, PointD leftTopPixel, bool isAnimating, bool isDarkTheme, MapProjection projection)
		{
			var intensity = Math.Clamp(RawIntensity, -3, 7);

			// 描画しない
			if (intensity < Config.MinShownIntensity)
				return;

			var circleSize = (float)(Math.Max(1, zoom - 4) * 1.75);
			var circleVector = new PointD(circleSize, circleSize);
			var pointCenter = Location.ToPixel(projection, zoom);
			if (!bound.IntersectsWith(new RectD(pointCenter - circleVector, pointCenter + circleVector)))
				return;

			// 観測点情報文字の描画
			if (!isAnimating && (zoom >= Config.ShowNameZoomLevel || zoom >= Config.ShowValueZoomLevel) && (!double.IsNaN(intensity) || Config.ShowInvalidateIcon))
			{
				// TODO キャッシュする
				if (textPaint == null)
					textPaint = new SKPaint
					{
						IsAntialias = true,
						Typeface = FixedObjectRenderer.MainTypeface,
						TextSize = 14,
						StrokeWidth = 2,
					};
				textPaint.TextSize = 14;// Math.Min(circleSize * 2, 14);
				var text = (zoom >= Config.ShowNameZoomLevel ? Name : "") + " " +
					(zoom >= Config.ShowValueZoomLevel ? (double.IsNaN(intensity) ? "-" : intensity.ToString("0.0")) : "");
				var point = (pointCenter - leftTopPixel + new PointD(circleSize, textPaint.TextSize * .4)).AsSKPoint();

				textPaint.Style = SKPaintStyle.Stroke;
				textPaint.Color = !isDarkTheme ? SKColors.White : SKColors.Black;
				canvas.DrawText(text, point, textPaint);
				textPaint.Style = SKPaintStyle.Fill;
				textPaint.Color = isDarkTheme ? SKColors.White : SKColors.Black;
				canvas.DrawText(text, point, textPaint);
			}

			var color = IntensityColor;
			// 震度アイコンの描画
			if (Config.ShowIntensityIcon)
			{
				if (intensity >= 0.5)
				{
					FixedObjectRenderer.DrawIntensity(canvas, JmaIntensityExtensions.ToJmaIntensity(intensity), (SKPoint)(pointCenter - leftTopPixel), circleSize * 2, true, true);
					return;
				}
				// 震度1未満であればモノクロに
				var num = (byte)(color.Red / 3 + color.Green / 3 + color.Blue / 3);
				color = new SKColor(num, num, num);
			}
			// 無効な観測点
			if (double.IsNaN(intensity))
			{
				// の描画
				if (Config.ShowInvalidateIcon)
				{
					if (invalidatePaint == null)
						invalidatePaint = new SKPaint
						{
							Style = SKPaintStyle.Stroke,
							IsAntialias = true,
							Color = SKColors.Gray,
							StrokeWidth = 1,
						};
					canvas.DrawCircle(
						(pointCenter - leftTopPixel).AsSKPoint(),
						circleSize,
						invalidatePaint);
				}
				return;
			}

			if (pointPaint == null)
				pointPaint = new SKPaint
				{
					Style = SKPaintStyle.Fill,
					IsAntialias = true,
					StrokeWidth = 1,
				};
			pointPaint.Color = color;
			// 観測点の色
			canvas.DrawCircle(
				(pointCenter - leftTopPixel).AsSKPoint(),
				circleSize,
				pointPaint);
		}
	}
}
