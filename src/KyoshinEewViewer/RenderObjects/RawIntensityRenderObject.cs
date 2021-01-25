using KyoshinEewViewer.CustomControls;
using KyoshinEewViewer.MapControl;
using KyoshinMonitorLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using static KyoshinEewViewer.Models.KyoshinEewViewerConfiguration;

namespace KyoshinEewViewer.RenderObjects
{
	public class RawIntensityRenderObject : IRenderObject
	{
		private static Typeface TypeFace { get; } = new Typeface(new FontFamily(new Uri("pack://application:,,,/"), "./Resources/#Gen Shin Gothic P Medium"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

		public string Name { get; }
		public RawIntensityRenderObject(RawIntensityObjectConfig config, Location location, string name, float rawIntensity = float.NaN)
		{
			Location = location ?? throw new ArgumentNullException(nameof(location));
			Config = config ?? throw new ArgumentNullException(nameof(config));
			RawIntensity = rawIntensity;
			Name = name;
		}

		/// <summary>
		/// 地理座標
		/// </summary>
		public Location Location { get; set; }

		/// <summary>
		/// 生の震度の値
		/// </summary>
		public double RawIntensity { get; set; }

		private RawIntensityObjectConfig Config { get; }

		private Color intensityColor;
		/// <summary>
		/// その地点の色
		/// </summary>
		public Color IntensityColor
		{
			get => intensityColor;
			set
			{
				if (intensityColor == value)
					return;
				intensityColor = value;
			}
		}
		private static Dictionary<Color, SolidColorBrush> BrushCache { get; } = new Dictionary<Color, SolidColorBrush>();
		static Pen InvalidatePen { get; set; }

		public void Render(DrawingContext context, Rect bound, double zoom, Point leftTopPixel, bool isDarkTheme)
		{
			var intensity = (float)Math.Min(Math.Max(RawIntensity, -3), 7.0);

			// 描画しない
			if (intensity < Config.MinShownIntensity)
				return;

			var circleSize = (zoom - 4) * 1.75;
			var circleVector = new Vector(circleSize, circleSize);
			var pointCenter = Location.ToPixel(zoom);
			if (!bound.IntersectsWith(new Rect(pointCenter - circleVector, pointCenter + circleVector)))
				return;

			// 観測点情報文字の描画
			if (zoom >= Config.ShowNameZoomLevel || zoom >= Config.ShowValueZoomLevel)
			{
				var text = new FormattedText(
					(zoom >= Config.ShowNameZoomLevel ? Name : "") +
					(zoom >= Config.ShowNameZoomLevel && zoom >= Config.ShowValueZoomLevel ? "\n" : "") +
					(zoom >= Config.ShowValueZoomLevel ? (float.IsNaN(intensity) ? "-" : intensity.ToString("0.0")) : ""),
					CultureInfo.CurrentCulture,
					FlowDirection.LeftToRight,
					TypeFace,
					16,
					isDarkTheme ? Brushes.White : Brushes.Black,
					94)
				{
					LineHeight = 14
				};
				context.DrawText(text, pointCenter - (Vector)leftTopPixel + new Vector(circleSize, -text.Height / 2));
			}

			// 震度アイコンの描画
			if (Config.ShowIntensityIcon && intensity >= 0.5)
			{
				FixedObjectRenderer.DrawIntensity(context, JmaIntensityExtensions.ToJmaIntensity((double)intensity), pointCenter - (Vector)leftTopPixel, circleSize * 2, true, true);
				return;
			}
			// 無効な観測点
			if (float.IsNaN(intensity))
			{
				// の描画
				if (Config.ShowInvalidateIcon)
				{
					// ブラシの初期化
					if (InvalidatePen == null)
					{
						InvalidatePen = new Pen(new SolidColorBrush(Colors.Gray), 1);
						InvalidatePen.Freeze();
					}
					context.DrawEllipse(
						null,
						InvalidatePen,
						pointCenter - (Vector)leftTopPixel,
						circleSize,
						circleSize);
				}
				return;
			}

			// 観測点色のブラシ
			if (!BrushCache.ContainsKey(IntensityColor) && !float.IsNaN(intensity))
			{
				var brush = new SolidColorBrush(IntensityColor);
				brush.Freeze();
				BrushCache[IntensityColor] = brush;
			}

			// 観測点の色
			context.DrawEllipse(
				BrushCache[IntensityColor],
				null,
				pointCenter - (Vector)leftTopPixel,
				circleSize,
				circleSize);
		}
	}
}