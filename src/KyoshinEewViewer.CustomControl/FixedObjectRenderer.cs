using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Skia;
using KyoshinEewViewer.Map;
using KyoshinMonitorLib;
using KyoshinMonitorLib.Images;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace KyoshinEewViewer.CustomControl
{
	public static class FixedObjectRenderer
	{
		static readonly SKTypeface face = SKTypeface.FromStream(AvaloniaLocator.Current.GetService<IAssetLoader>().Open(new Uri("avares://KyoshinEewViewer.Core/Assets/Fonts/GenShinGothic-P-Medium.ttf", UriKind.Absolute)));
		static readonly SKTypeface intensityFace = SKTypeface.FromStream(AvaloniaLocator.Current.GetService<IAssetLoader>().Open(new Uri("avares://KyoshinEewViewer.Core/Assets/Fonts/GenShinGothic-P-Bold.ttf", UriKind.Absolute)));

		public const double INTENSITY_WIDE_SCALE = .75;

		static ConcurrentDictionary<JmaIntensity, (SKPaint b, SKPaint f)> IntensityPaintCache { get; } = new();
		static SKPaint? ForegroundPaint { get; set; }
		static SKPaint? SubForegroundPaint { get; set; }

		public static bool PaintCacheInitalized { get; private set; }

		public static void UpdateIntensityPaintCache(Control control)
		{
			SKColor FindColorResource(string name)
				=> ((Color)(control.FindResource(name) ?? throw new Exception($"震度リソース {name} が見つかりませんでした"))).ToSKColor();
			//float FindFloatResource(string name)
			//	=> (float)(control.FindResource(name) ?? throw new Exception($"震度リソース {name} が見つかりませんでした"));

			if (ForegroundPaint is SKPaint paintf)
				paintf.Dispose();
			ForegroundPaint = new SKPaint
			{
				Style = SKPaintStyle.Fill,
				Color = FindColorResource("ForegroundColor"),
				Typeface = face,
				IsAntialias = true,
			};
			if (SubForegroundPaint is SKPaint paints)
				paints.Dispose();
			SubForegroundPaint = new SKPaint
			{
				Style = SKPaintStyle.Fill,
				Color = FindColorResource("SubForegroundColor"),
				IsAntialias = true,
			};

			foreach (JmaIntensity i in Enum.GetValues<JmaIntensity>())
			{
				var b = new SKPaint
				{
					Style = SKPaintStyle.Fill,
					Color = FindColorResource(i + "Background"),
					IsAntialias = true,
				};
				var f = new SKPaint
				{
					Style = SKPaintStyle.Fill,
					Color = FindColorResource(i + "Foreground"),
					Typeface = intensityFace,
					IsAntialias = true,
				};

				IntensityPaintCache.AddOrUpdate(i, (b, f), (v, c) =>
				{
					c.b.Dispose();
					c.f.Dispose();
					return (b, f);
				});
			}
			PaintCacheInitalized = true;
		}

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
		public static void DrawIntensity(this SKCanvas canvas, JmaIntensity intensity, PointD point, float size, bool centerPosition = false, bool circle = false, bool wide = false)
		{
			if (!IntensityPaintCache.TryGetValue(intensity, out var paints))
				return;

			var halfSize = new PointD(size / 2, size / 2);
			if (wide)
				halfSize.X /= INTENSITY_WIDE_SCALE;
			var leftTop = centerPosition ? point - halfSize : point;

			if (circle && !wide)
				canvas.DrawCircle((centerPosition ? point : (point + halfSize)).AsSKPoint(), (float)(size / 2), paints.b);
			else
				canvas.DrawRect((float)leftTop.X, (float)leftTop.Y, (float)(wide ? size / INTENSITY_WIDE_SCALE : size), (float)size, paints.b);

			switch (intensity)
			{
				case JmaIntensity.Int5Lower:
					{
						if (size < 12)
						{
							paints.f.TextSize = (float)(size * 1.25);
							canvas.DrawText("-", new PointD(leftTop.X + size * .25, leftTop.Y + size * .5).AsSKPoint(), paints.f);
							break;
						}
						paints.f.TextSize = (float)size;
						canvas.DrawText("5", new PointD(leftTop.X + size * .1, leftTop.Y + size * .86).AsSKPoint(), paints.f);
						if (wide)
						{
							paints.f.TextSize = (float)(size * .55);
							canvas.DrawText("弱", new PointD(leftTop.X + size * .65, leftTop.Y + size * .85).AsSKPoint(), paints.f);
						}
						else
						{
							paints.f.TextSize = (float)(size * .75);
							canvas.DrawText("-", new PointD(leftTop.X + size * .6, leftTop.Y + size * .5).AsSKPoint(), paints.f);
						}
					}
					return;
				case JmaIntensity.Int5Upper:
					{
						if (size < 12)
						{
							paints.f.TextSize = (float)(size * 1.25);
							canvas.DrawText("+", new PointD(leftTop.X + size * .1, leftTop.Y + size * .6).AsSKPoint(), paints.f);
							break;
						}
						paints.f.TextSize = (float)size;
						canvas.DrawText("5", new PointD(leftTop.X + size * .1, leftTop.Y + size * .86).AsSKPoint(), paints.f);
						if (wide)
						{
							paints.f.TextSize = (float)(size * .55);
							canvas.DrawText("強", new PointD(leftTop.X + size * .65, leftTop.Y + size * .85).AsSKPoint(), paints.f);
						}
						else
						{
							paints.f.TextSize = (float)(size * .75);
							canvas.DrawText("+", new PointD(leftTop.X + size * .5, leftTop.Y + size * .6).AsSKPoint(), paints.f);
						}
					}
					return;
				case JmaIntensity.Int6Lower:
					{
						if (size < 12)
						{
							paints.f.TextSize = (float)(size * 1.25);
							canvas.DrawText("-", new PointD(leftTop.X + size * .25, leftTop.Y + size * .5).AsSKPoint(), paints.f);
							break;
						}
						paints.f.TextSize = (float)size;
						canvas.DrawText("6", new PointD(leftTop.X + size * .1, leftTop.Y + size * .86).AsSKPoint(), paints.f);
						if (wide)
						{
							paints.f.TextSize = (float)(size * .55);
							canvas.DrawText("弱", new PointD(leftTop.X + size * .65, leftTop.Y + size * .85).AsSKPoint(), paints.f);
						}
						else
						{
							paints.f.TextSize = (float)(size * .75);
							canvas.DrawText("-", new PointD(leftTop.X + size * .6, leftTop.Y + size * .5).AsSKPoint(), paints.f);
						}
					}
					return;
				case JmaIntensity.Int6Upper:
					{
						if (size < 12)
						{
							paints.f.TextSize = (float)(size * 1.25);
							canvas.DrawText("+", new PointD(leftTop.X + size * .1, leftTop.Y + size * .6).AsSKPoint(), paints.f);
							break;
						}
						paints.f.TextSize = (float)size;
						canvas.DrawText("6", new PointD(leftTop.X + size * .1, leftTop.Y + size * .86).AsSKPoint(), paints.f);
						if (wide)
						{
							paints.f.TextSize = (float)(size * .55);
							canvas.DrawText("強", new PointD(leftTop.X + size * .65, leftTop.Y + size * .85).AsSKPoint(), paints.f);
						}
						else
						{
							paints.f.TextSize = (float)(size * .75);
							canvas.DrawText("+", new PointD(leftTop.X + size * .5, leftTop.Y + size * .6).AsSKPoint(), paints.f);
						}
					}
					return;
				case JmaIntensity.Unknown:
					paints.f.TextSize = (float)size;
					canvas.DrawText("-", new PointD(leftTop.X + size * (wide ? .52 : .32), leftTop.Y + size * .8).AsSKPoint(), paints.f);
					return;
				case JmaIntensity.Error:
					paints.f.TextSize = (float)size;
					canvas.DrawText("E", new PointD(leftTop.X + size * (wide ? .35 : .18), leftTop.Y + size * .87).AsSKPoint(), paints.f);
					return;
			}
			if (size >= 12)
			{
				paints.f.TextSize = (float)size;
				canvas.DrawText(intensity.ToShortString(), new PointD(leftTop.X + size * (wide ? .38 : .22), leftTop.Y + size * .86).AsSKPoint(), paints.f);
			}
		}

		public static void DrawLinkedRealtimeData(this SKCanvas canvas, IEnumerable<ImageAnalysisResult>? points, float itemHeight, float firstHeight, float maxWidth, float maxHeight, bool useShindoIcon = true)
		{
			if (points == null || ForegroundPaint == null || SubForegroundPaint == null) return;

			int count = 0;
			var verticalOffset = 0f;
			foreach (var point in points)
			{
				var horizontalOffset = 0f;
				var height = count == 0 ? firstHeight : itemHeight;
				if (useShindoIcon)
				{
					canvas.DrawIntensity(point.GetResultToIntensity().ToJmaIntensity(), new PointD(0, verticalOffset), height);
					horizontalOffset += height;
				}
				else
				{
					using var rectPaint = new SKPaint 
					{
						Style = SKPaintStyle.Fill,
						Color = new SKColor(point.Color.R, point.Color.G, point.Color.B, point.Color.A),
					};
					canvas.DrawRect(0, verticalOffset, height / 5, height, rectPaint);
					horizontalOffset += height / 5;
				}

				var region = point.ObservationPoint.Region;
				if (region.Contains(' '))
					region = region.Substring(0, region.IndexOf(' '));

				ForegroundPaint.TextSize = itemHeight * .6f;
				canvas.DrawText(region, horizontalOffset + height * 0.1f, verticalOffset + height * .9f, ForegroundPaint);
				horizontalOffset += Math.Max(ForegroundPaint.MeasureText(region), maxWidth / 4);

				ForegroundPaint.TextSize = itemHeight * .75f;
				ForegroundPaint.Typeface = intensityFace;
				canvas.DrawText(point.ObservationPoint.Name, horizontalOffset + height * 0.2f, verticalOffset + height * .9f, ForegroundPaint);

				ForegroundPaint.TextSize = Math.Min(height * .4f, itemHeight * .75f);
				ForegroundPaint.Typeface = face;
				ForegroundPaint.TextAlign = SKTextAlign.Right;
				canvas.DrawText(point.GetResultToIntensity()?.ToString("0.0") ?? "?", maxWidth, verticalOffset + height, ForegroundPaint);

				ForegroundPaint.TextAlign = SKTextAlign.Left;

				count++;
				verticalOffset += height;
				if (verticalOffset >= maxHeight)
					return;
			}
		}
	}
}
