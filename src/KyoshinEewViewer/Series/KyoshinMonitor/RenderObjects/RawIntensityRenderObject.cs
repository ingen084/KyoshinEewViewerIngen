using Avalonia;
using Avalonia.Platform;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Projections;
using KyoshinEewViewer.Services;
using KyoshinMonitorLib;
using SkiaSharp;
using System;
using static KyoshinEewViewer.Core.Models.KyoshinEewViewerConfiguration;

namespace KyoshinEewViewer.Series.KyoshinMonitor.RenderObjects
{
	public class RawIntensityRenderObject : IRenderObject
	{
		static readonly SKTypeface face = SKTypeface.FromStream(AvaloniaLocator.Current.GetService<IAssetLoader>().Open(new Uri("avares://KyoshinEewViewer.Core/Assets/Fonts/GenShinGothic-P-Medium.ttf", UriKind.Absolute)));
		public string Name { get; }
		public RawIntensityRenderObject(Location location, string name, float rawIntensity = float.NaN)
		{
			Location = location ?? throw new ArgumentNullException(nameof(location));
			Config = ConfigurationService.Default.RawIntensityObject;
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

		private SKColor intensityColor;
		/// <summary>
		/// その地点の色
		/// </summary>
		public SKColor IntensityColor
		{
			get => intensityColor;
			set
			{
				if (intensityColor == value)
					return;
				intensityColor = value;
			}
		}

		public void Render(SKCanvas canvas, RectD bound, double zoom, PointD leftTopPixel, bool isDarkTheme, MapProjection projection)
		{
			var intensity = Math.Clamp(RawIntensity, -3, 7);

			// 描画しない
			if (intensity < Config.MinShownIntensity)
				return;

			var circleSize = (float)((zoom - 4) * 1.75);
			var circleVector = new PointD(circleSize, circleSize);
			var pointCenter = Location.ToPixel(projection, zoom);
			if (!bound.IntersectsWith(new RectD(pointCenter - circleVector, pointCenter + circleVector)))
				return;

			// 観測点情報文字の描画
			if (zoom >= Config.ShowNameZoomLevel || zoom >= Config.ShowValueZoomLevel)
			{
				var multiLine = zoom >= Config.ShowNameZoomLevel && zoom >= Config.ShowValueZoomLevel;
				using var textPaint = new SKPaint
				{
					Style = SKPaintStyle.Fill,
					IsAntialias = true,
					Typeface = face,
					TextSize = 14,
					Color = isDarkTheme ? SKColors.White : SKColors.Black
				};
				canvas.DrawText((zoom >= Config.ShowNameZoomLevel ? Name : "") +
					(multiLine ? "\n" : "") +
					(zoom >= Config.ShowValueZoomLevel ? (double.IsNaN(intensity) ? "-" : intensity.ToString("0.0")) : ""), (pointCenter - leftTopPixel + new PointD(circleSize, multiLine ? -11 : -4)).AsSKPoint(), textPaint);
			}

			var color = IntensityColor;
			// 震度アイコンの描画
			if (Config.ShowIntensityIcon)
			{
				if (intensity >= 0.5)
				{
					FixedObjectRenderer.DrawIntensity(canvas, JmaIntensityExtensions.ToJmaIntensity(intensity), pointCenter - leftTopPixel, circleSize * 2, true, true);
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
					using var invalidatePaint = new SKPaint
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

			// TODO キャッシュする
			using var paint = new SKPaint
			{
				Style = SKPaintStyle.Fill,
				IsAntialias = true,
				Color = color,
				StrokeWidth = 1,
			};
			// 観測点の色
			canvas.DrawCircle(
				(pointCenter - leftTopPixel).AsSKPoint(),
				circleSize,
				paint);
		}
	}
}
