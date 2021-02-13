using KyoshinEewViewer.CustomControls;
using KyoshinEewViewer.MapControl;
using KyoshinEewViewer.MapControl.Projections;
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

		public void Render(DrawingContext context, Rect bound, double zoom, Point leftTopPixel, bool isDarkTheme, MapProjection projection)
		{
			var intensity = Math.Clamp(RawIntensity, -3, 7);

			// 描画しない
			if (intensity < Config.MinShownIntensity)
				return;

			var circleSize = (zoom - 4) * 1.75;
			var circleVector = new Vector(circleSize, circleSize);
			var pointCenter = Location.ToPixel(projection, zoom);
			if (!bound.IntersectsWith(new Rect(pointCenter - circleVector, pointCenter + circleVector)))
				return;

			// 観測点情報文字の描画
			if (zoom >= Config.ShowNameZoomLevel || zoom >= Config.ShowValueZoomLevel)
			{
				var multiLine = zoom >= Config.ShowNameZoomLevel && zoom >= Config.ShowValueZoomLevel;
				var text = new FormattedText(
					(zoom >= Config.ShowNameZoomLevel ? Name : "") +
					(multiLine ? "\n" : "") +
					(zoom >= Config.ShowValueZoomLevel ? (double.IsNaN(intensity) ? "-" : intensity.ToString("0.0")) : ""),
					CultureInfo.CurrentCulture,
					FlowDirection.LeftToRight,
					TypeFace,
					14,
					isDarkTheme ? Brushes.White : Brushes.Black,
					96)
				{
					LineHeight = 14
				};
				context.DrawText(text, pointCenter - (Vector)leftTopPixel + new Vector(circleSize, multiLine ? -11 : -4));
			}

			var color = IntensityColor;
			// 震度アイコンの描画
			if (Config.ShowIntensityIcon)
			{
				if (intensity >= 0.5)
				{
					FixedObjectRenderer.DrawIntensity(context, JmaIntensityExtensions.ToJmaIntensity(intensity), pointCenter - (Vector)leftTopPixel, circleSize * 2, true, true);
					return;
				}
				// 震度1未満であればモノクロに
				var num = (byte)(color.R / 3 + color.G / 3 + color.B / 3);
				color = Color.FromRgb(num, num, num);
			}
			// 無効な観測点
			if (double.IsNaN(intensity))
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
			if (!BrushCache.ContainsKey(color))
			{
				var brush = new SolidColorBrush(color);
				brush.Freeze();
				BrushCache[color] = brush;
			}

			// 観測点の色
			context.DrawEllipse(
				BrushCache[color],
				null,
				pointCenter - (Vector)leftTopPixel,
				circleSize,
				circleSize);
		}
	}
}