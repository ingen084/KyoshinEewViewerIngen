using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Skia;
using KyoshinEewViewer.Core;
using KyoshinMonitorLib;
using SkiaSharp;
using System;
using System.Collections.Concurrent;

namespace KyoshinEewViewer.CustomControl;

public static class FixedObjectRenderer
{
	public const double IntensityWideScale = .75;

	public static ConcurrentDictionary<JmaIntensity, (SKPaint Background, SKPaint Foreground, SKPaint Border)> IntensityPaintCache { get; } = new();
	public static ConcurrentDictionary<LpgmIntensity, (SKPaint Background, SKPaint Foreground, SKPaint Border)> LpgmIntensityPaintCache { get; } = new();
	private static SKPaint? ForegroundPaint { get; set; }
	private static SKPaint? SubForegroundPaint { get; set; }

	public static bool PaintCacheInitialized { get; private set; }

	public static void UpdateIntensityPaintCache(Control control)
	{
		SKColor FindColorResource(string name)
			=> ((Color)(control.FindResource(name) ?? throw new Exception($"震度リソース {name} が見つかりませんでした"))).ToSKColor();

		ForegroundPaint?.Dispose();
		ForegroundPaint = new SKPaint
		{
			Style = SKPaintStyle.Fill,
			Color = FindColorResource("ForegroundColor"),
			Typeface = KyoshinEewViewerFonts.MainRegular,
			IsAntialias = true,
			SubpixelText = true,
			LcdRenderText = true,
		};
		SubForegroundPaint?.Dispose();
		SubForegroundPaint = new SKPaint
		{
			Style = SKPaintStyle.Fill,
			Color = FindColorResource("SubForegroundColor"),
			IsAntialias = true,
			SubpixelText = true,
			LcdRenderText = true,
		};

		foreach (var i in Enum.GetValues<JmaIntensity>())
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
				Typeface = KyoshinEewViewerFonts.MainBold,
				IsAntialias = true,
				SubpixelText = true,
				LcdRenderText = true,
			};
			var b2 = new SKPaint
			{
				Style = SKPaintStyle.Stroke,
				Color = FindColorResource(i + "Border"),
				StrokeWidth = 1,
				IsAntialias = true,
			};

			IntensityPaintCache.AddOrUpdate(i, (b, f, b2), (v, c) =>
			{
				c.Background.Dispose();
				c.Foreground.Dispose();
				c.Border.Dispose();
				return (b, f, b2);
			});
		}

		foreach (var i in Enum.GetValues<LpgmIntensity>())
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
				Typeface = KyoshinEewViewerFonts.MainBold,
				IsAntialias = true,
				SubpixelText = true,
				LcdRenderText = true,
			};
			var b2 = new SKPaint
			{
				Style = SKPaintStyle.Stroke,
				Color = FindColorResource(i + "Border"),
				StrokeWidth = 1,
				IsAntialias = true,
			};

			LpgmIntensityPaintCache.AddOrUpdate(i, (b, f, b2), (v, c) =>
			{
				c.Background.Dispose();
				c.Foreground.Dispose();
				c.Border.Dispose();
				return (b, f, b2);
			});
		}
		PaintCacheInitialized = true;
	}

	/// <summary>
	/// 震度アイコンを描画する
	/// </summary>
	/// <param name="canvas">描画先のDrawingContext</param>
	/// <param name="intensity">描画する震度</param>
	/// <param name="point">座標</param>
	/// <param name="size">描画するサイズ ワイドモードの場合縦サイズになる</param>
	/// <param name="centerPosition">指定した座標を中心座標にするか</param>
	/// <param name="circle">縁を円形にするか wideがfalseのときのみ有効</param>
	/// <param name="wide">ワイドモード(強弱漢字表記)にするか</param>
	/// <param name="round">縁を丸めるか circleがfalseのときのみ有効</param>
	/// <param name="border">縁を用意するか</param>
	public static void DrawIntensity(this SKCanvas canvas, JmaIntensity intensity, SKPoint point, float size, bool centerPosition = false, bool circle = false, bool wide = false, bool round = false, bool border = false)
	{
		if (!IntensityPaintCache.TryGetValue(intensity, out var paints))
			return;

		var halfSize = new PointD(size / 2, size / 2);
		if (wide)
			halfSize.X /= IntensityWideScale;
		var leftTop = centerPosition ? point - halfSize : (PointD)point;

		paints.Border.StrokeWidth = size / 8;

		if (circle && !wide)
		{
			canvas.DrawCircle(centerPosition ? point : (SKPoint)(point + halfSize), size / 2, paints.Background);
			if (border)
				canvas.DrawCircle(centerPosition ? point : (SKPoint)(point + halfSize), size / 2, paints.Border);
		}
		else if (round)
		{
			canvas.DrawRoundRect((float)leftTop.X, (float)leftTop.Y, (float)(wide ? size / IntensityWideScale : size), size, size * .2f, size * .2f, paints.Background);
			if (border)
				canvas.DrawRoundRect((float)leftTop.X, (float)leftTop.Y, (float)(wide ? size / IntensityWideScale : size), size, size * .2f, size * .2f, paints.Border);
		}
		else
		{
			canvas.DrawRect((float)leftTop.X, (float)leftTop.Y, (float)(wide ? size / IntensityWideScale : size), size, paints.Background);
			if (border)
				canvas.DrawRect((float)leftTop.X, (float)leftTop.Y, (float)(wide ? size / IntensityWideScale : size), size, paints.Border);
		}

		switch (intensity)
		{
			case JmaIntensity.Int1:
				if (size >= 8)
				{
					paints.Foreground.TextSize = size;
					canvas.DrawText(intensity.ToShortString(), new PointD(leftTop.X + size * (wide ? .38 : .2), leftTop.Y + size * .87).AsSkPoint(), paints.Foreground);
				}
				return;
			case JmaIntensity.Int4:
				if (size >= 8)
				{
					paints.Foreground.TextSize = size;
					canvas.DrawText(intensity.ToShortString(), new PointD(leftTop.X + size * (wide ? .38 : .19), leftTop.Y + size * .87).AsSkPoint(), paints.Foreground);
				}
				return;
			case JmaIntensity.Int7:
				if (size >= 8)
				{
					paints.Foreground.TextSize = size;
					canvas.DrawText(intensity.ToShortString(), new PointD(leftTop.X + size * (wide ? .38 : .22), leftTop.Y + size * .89).AsSkPoint(), paints.Foreground);
				}
				return;
			case JmaIntensity.Int5Lower:
				{
					if (size < 8)
					{
						paints.Foreground.TextSize = (float)(size * 1.25);
						canvas.DrawText("-", new PointD(leftTop.X + size * .25, leftTop.Y + size * .8).AsSkPoint(), paints.Foreground);
						break;
					}
					paints.Foreground.TextSize = size;
					canvas.DrawText("5", new PointD(leftTop.X + size * .1, leftTop.Y + size * .87).AsSkPoint(), paints.Foreground);
					if (wide)
					{
						paints.Foreground.TextSize = (float)(size * .55);
						canvas.DrawText("弱", new PointD(leftTop.X + size * .65, leftTop.Y + size * .85).AsSkPoint(), paints.Foreground);
					}
					else
					{
						paints.Foreground.TextSize = size;
						canvas.DrawText("-", new PointD(leftTop.X + size * .6, leftTop.Y + size * .6).AsSkPoint(), paints.Foreground);
					}
				}
				return;
			case JmaIntensity.Int5Upper:
				{
					if (size < 8)
					{
						paints.Foreground.TextSize = (float)(size * 1.25);
						canvas.DrawText("+", new PointD(leftTop.X + size * .1, leftTop.Y + size * .8).AsSkPoint(), paints.Foreground);
						break;
					}
					paints.Foreground.TextSize = size;
					canvas.DrawText("5", new PointD(leftTop.X + size * .1, leftTop.Y + size * .87).AsSkPoint(), paints.Foreground);
					if (wide)
					{
						paints.Foreground.TextSize = (float)(size * .55);
						canvas.DrawText("強", new PointD(leftTop.X + size * .65, leftTop.Y + size * .85).AsSkPoint(), paints.Foreground);
					}
					else
					{
						paints.Foreground.TextSize = (float)(size * .8);
						canvas.DrawText("+", new PointD(leftTop.X + size * .5, leftTop.Y + size * .65).AsSkPoint(), paints.Foreground);
					}
				}
				return;
			case JmaIntensity.Int6Lower:
				{
					if (size < 8)
					{
						paints.Foreground.TextSize = (float)(size * 1.25);
						canvas.DrawText("-", new PointD(leftTop.X + size * .25, leftTop.Y + size * .8).AsSkPoint(), paints.Foreground);
						break;
					}
					paints.Foreground.TextSize = size;
					canvas.DrawText("6", new PointD(leftTop.X + size * .1, leftTop.Y + size * .86).AsSkPoint(), paints.Foreground);
					if (wide)
					{
						paints.Foreground.TextSize = (float)(size * .55);
						canvas.DrawText("弱", new PointD(leftTop.X + size * .65, leftTop.Y + size * .85).AsSkPoint(), paints.Foreground);
					}
					else
					{
						paints.Foreground.TextSize = size;
						canvas.DrawText("-", new PointD(leftTop.X + size * .6, leftTop.Y + size * .6).AsSkPoint(), paints.Foreground);
					}
				}
				return;
			case JmaIntensity.Int6Upper:
				{
					if (size < 8)
					{
						paints.Foreground.TextSize = (float)(size * 1.25);
						canvas.DrawText("+", new PointD(leftTop.X + size * .1, leftTop.Y + size * .8).AsSkPoint(), paints.Foreground);
						break;
					}
					paints.Foreground.TextSize = size;
					canvas.DrawText("6", new PointD(leftTop.X + size * .1, leftTop.Y + size * .86).AsSkPoint(), paints.Foreground);
					if (wide)
					{
						paints.Foreground.TextSize = (float)(size * .55);
						canvas.DrawText("強", new PointD(leftTop.X + size * .65, leftTop.Y + size * .85).AsSkPoint(), paints.Foreground);
					}
					else
					{
						paints.Foreground.TextSize = (float)(size * .8);
						canvas.DrawText("+", new PointD(leftTop.X + size * .5, leftTop.Y + size * .65).AsSkPoint(), paints.Foreground);
					}
				}
				return;
			case JmaIntensity.Unknown:
				paints.Foreground.TextSize = size;
				canvas.DrawText("-", new PointD(leftTop.X + size * (wide ? .52 : .32), leftTop.Y + size * .8).AsSkPoint(), paints.Foreground);
				return;
			case JmaIntensity.Error:
				paints.Foreground.TextSize = size;
				canvas.DrawText("E", new PointD(leftTop.X + size * (wide ? .35 : .18), leftTop.Y + size * .88).AsSkPoint(), paints.Foreground);
				return;
		}
		if (size >= 8)
		{
			paints.Foreground.TextSize = size;
			canvas.DrawText(intensity.ToShortString(), new PointD(leftTop.X + size * (wide ? .38 : .22), leftTop.Y + size * .87).AsSkPoint(), paints.Foreground);
		}
	}


	/// <summary>
	/// 長周期地震動階級のアイコンを描画する
	/// </summary>
	/// <param name="canvas">描画先のDrawingContext</param>
	/// <param name="intensity">描画する震度</param>
	/// <param name="point">座標</param>
	/// <param name="size">描画するサイズ ワイドモードの場合縦サイズになる</param>
	/// <param name="centerPosition">指定した座標を中心座標にするか</param>
	/// <param name="circle">縁を円形にするか wideがfalseのときのみ有効</param>
	/// <param name="wide">ワイドモード(強弱漢字表記)にするか</param>
	/// <param name="round">縁を丸めるか circleがfalseのときのみ有効</param>
	/// <param name="border">縁を用意するか</param>
	public static void DrawLpgmIntensity(this SKCanvas canvas, LpgmIntensity intensity, SKPoint point, float size, bool centerPosition = false, bool circle = false, bool wide = false, bool round = false, bool border = false)
	{
		if (!LpgmIntensityPaintCache.TryGetValue(intensity, out var paints))
			return;

		var halfSize = new PointD(size / 2, size / 2);
		if (wide)
			halfSize.X /= IntensityWideScale;
		var leftTop = centerPosition ? point - halfSize : (PointD)point;

		paints.Border.StrokeWidth = size / 8;

		if (circle && !wide)
		{
			canvas.DrawCircle(centerPosition ? point : (SKPoint)(point + halfSize), size / 2, paints.Background);
			if (border)
				canvas.DrawCircle(centerPosition ? point : (SKPoint)(point + halfSize), size / 2, paints.Border);
		}
		else if (round)
		{
			canvas.DrawRoundRect((float)leftTop.X, (float)leftTop.Y, (float)(wide ? size / IntensityWideScale : size), size, size * .2f, size * .2f, paints.Background);
			if (border)
				canvas.DrawRoundRect((float)leftTop.X, (float)leftTop.Y, (float)(wide ? size / IntensityWideScale : size), size, size * .2f, size * .2f, paints.Border);
		}
		else
		{
			canvas.DrawRect((float)leftTop.X, (float)leftTop.Y, (float)(wide ? size / IntensityWideScale : size), size, paints.Background);
			if (border)
				canvas.DrawRect((float)leftTop.X, (float)leftTop.Y, (float)(wide ? size / IntensityWideScale : size), size, paints.Border);
		}

		switch (intensity)
		{
			case LpgmIntensity.LpgmInt1:
				if (size >= 8)
				{
					paints.Foreground.TextSize = size;
					canvas.DrawText(intensity.ToShortString(), new PointD(leftTop.X + size * (wide ? .38 : .2), leftTop.Y + size * .87).AsSkPoint(), paints.Foreground);
				}
				return;
			case LpgmIntensity.LpgmInt4:
				if (size >= 8)
				{
					paints.Foreground.TextSize = size;
					canvas.DrawText(intensity.ToShortString(), new PointD(leftTop.X + size * (wide ? .38 : .19), leftTop.Y + size * .87).AsSkPoint(), paints.Foreground);
				}
				return;
			case LpgmIntensity.Unknown:
				paints.Foreground.TextSize = size;
				canvas.DrawText("-", new PointD(leftTop.X + size * (wide ? .52 : .32), leftTop.Y + size * .8).AsSkPoint(), paints.Foreground);
				return;
			case LpgmIntensity.Error:
				paints.Foreground.TextSize = size;
				canvas.DrawText("E", new PointD(leftTop.X + size * (wide ? .35 : .18), leftTop.Y + size * .88).AsSkPoint(), paints.Foreground);
				return;
		}
		if (size >= 8)
		{
			paints.Foreground.TextSize = size;
			canvas.DrawText(intensity.ToShortString(), new PointD(leftTop.X + size * (wide ? .38 : .22), leftTop.Y + size * .87).AsSkPoint(), paints.Foreground);
		}
	}
}
