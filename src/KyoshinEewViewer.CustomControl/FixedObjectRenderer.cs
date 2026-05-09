using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Skia;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
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
	// 震度アイコンの文字描画に使用するフォント。テーマに依存しないため静的に保持する
	private static readonly SKFont IntensityFont = new(KyoshinEewViewerFonts.Inter)
	{
		Subpixel = true,
		Edging = SKFontEdging.SubpixelAntialias,
	};
	private static readonly SKFont IntensitySubFont = new(KyoshinEewViewerFonts.MainBold)
	{
		Subpixel = true,
		Edging = SKFontEdging.SubpixelAntialias,
	};
	private static float BorderMultiply { get; set; } = 0.125f;

	public static bool PaintCacheInitialized { get; private set; }

	public static void UpdateIntensityPaintCache(IResourceHost control)
	{
		SKColor FindColorResource(string name)
			=> ((Color)(control.FindResource(name) ?? throw new Exception($"震度リソース {name} が見つかりませんでした"))).ToSKColor();
		float FindFloatResource(string name)
			=> (float)(control.FindResource(name) ?? throw new Exception($"リソース {name} が見つかりませんでした"));

		BorderMultiply = FindFloatResource("BorderWidthMultiply");

		ForegroundPaint?.Dispose();
		ForegroundPaint = new SKPaint
		{
			Style = SKPaintStyle.Fill,
			Color = FindColorResource("ForegroundColor"),
			IsAntialias = true,
		};
		SubForegroundPaint?.Dispose();
		SubForegroundPaint = new SKPaint
		{
			Style = SKPaintStyle.Fill,
			Color = FindColorResource("SubForegroundColor"),
			IsAntialias = true,
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
				IsAntialias = true,
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
				IsAntialias = true,
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
		var leftTop = centerPosition ? (PointD)point - halfSize : (PointD)point;

		paints.Border.StrokeWidth = size * BorderMultiply;

		if (circle && !wide)
		{
			canvas.DrawCircle(centerPosition ? point : (SKPoint)((PointD)point + halfSize), size / 2, paints.Background);
			if (border)
				canvas.DrawCircle(centerPosition ? point : (SKPoint)((PointD)point + halfSize), size / 2, paints.Border);
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
			case JmaIntensity.Int0:
				if (size >= 8)
				{
					IntensityFont.Size = size;
					canvas.DrawText(intensity.ToShortString(), new PointD(leftTop.X + size * (wide ? .33 : .155), leftTop.Y + size * .87).AsSkPoint(), SKTextAlign.Left, IntensityFont, paints.Foreground);
				}
				return;
			case JmaIntensity.Int1:
				if (size >= 8)
				{
					IntensityFont.Size = size;
					canvas.DrawText(intensity.ToShortString(), new PointD(leftTop.X + size * (wide ? .445 : .25), leftTop.Y + size * .87).AsSkPoint(), SKTextAlign.Left, IntensityFont, paints.Foreground);
				}
				return;
			case JmaIntensity.Int4:
				if (size >= 8)
				{
					IntensityFont.Size = size;
					canvas.DrawText(intensity.ToShortString(), new PointD(leftTop.X + size * (wide ? .31 : .14), leftTop.Y + size * .87).AsSkPoint(), SKTextAlign.Left, IntensityFont, paints.Foreground);
				}
				return;
			case JmaIntensity.Int7:
				if (size >= 8)
				{
					IntensityFont.Size = size;
					canvas.DrawText(intensity.ToShortString(), new PointD(leftTop.X + size * (wide ? .39 : .22), leftTop.Y + size * .89).AsSkPoint(), SKTextAlign.Left, IntensityFont, paints.Foreground);
				}
				return;
			case JmaIntensity.Int5Lower:
				{
					if (size < 8)
					{
						IntensityFont.Size = (float)(size * 1.25);
						canvas.DrawText("-", new PointD(leftTop.X + size * .25, leftTop.Y + size * .8).AsSkPoint(), SKTextAlign.Left, IntensityFont, paints.Foreground);
						break;
					}
					IntensityFont.Size = size;
					canvas.DrawText("5", new PointD(leftTop.X + size * (wide ? .08 : .06), leftTop.Y + size * .87).AsSkPoint(), SKTextAlign.Left, IntensityFont, paints.Foreground);
					if (wide)
					{
						IntensitySubFont.Size = (float)(size * .55);
						canvas.DrawText("弱", new PointD(leftTop.X + size * .67, leftTop.Y + size * .84).AsSkPoint(), SKTextAlign.Left, IntensitySubFont, paints.Foreground);
					}
					else
					{
						IntensityFont.Size = size;
						canvas.DrawText("-", new PointD(leftTop.X + size * .5, leftTop.Y + size * .64).AsSkPoint(), SKTextAlign.Left, IntensityFont, paints.Foreground);
					}
				}
				return;
			case JmaIntensity.Int5Upper:
				{
					if (size < 8)
					{
						IntensityFont.Size = (float)(size * 1.25);
						canvas.DrawText("+", new PointD(leftTop.X + size * .1, leftTop.Y + size * .8).AsSkPoint(), SKTextAlign.Left, IntensityFont, paints.Foreground);
						break;
					}
					IntensityFont.Size = size;
					canvas.DrawText("5", new PointD(leftTop.X + size * (wide ? .08 : .06), leftTop.Y + size * .87).AsSkPoint(), SKTextAlign.Left, IntensityFont, paints.Foreground);
					if (wide)
					{
						IntensitySubFont.Size = (float)(size * .57);
						canvas.DrawText("強", new PointD(leftTop.X + size * .66, leftTop.Y + size * .84).AsSkPoint(), SKTextAlign.Left, IntensitySubFont, paints.Foreground);
					}
					else
					{
						IntensityFont.Size = (float)(size * .9);
						canvas.DrawText("+", new PointD(leftTop.X + size * .43, leftTop.Y + size * .58).AsSkPoint(), SKTextAlign.Left, IntensityFont, paints.Foreground);
					}
				}
				return;
			case JmaIntensity.Int6Lower:
				{
					if (size < 8)
					{
						IntensityFont.Size = (float)(size * 1.25);
						canvas.DrawText("-", new PointD(leftTop.X + size * .25, leftTop.Y + size * .8).AsSkPoint(), SKTextAlign.Left, IntensityFont, paints.Foreground);
						break;
					}
					IntensityFont.Size = size;
					canvas.DrawText("6", new PointD(leftTop.X + size * (wide ? .07 : .04), leftTop.Y + size * .86).AsSkPoint(), SKTextAlign.Left, IntensityFont, paints.Foreground);
					if (wide)
					{
						IntensitySubFont.Size = (float)(size * .55);
						canvas.DrawText("弱", new PointD(leftTop.X + size * .67, leftTop.Y + size * .84).AsSkPoint(), SKTextAlign.Left, IntensitySubFont, paints.Foreground);
					}
					else
					{
						IntensityFont.Size = size;
						canvas.DrawText("-", new PointD(leftTop.X + size * .55, leftTop.Y + size * .68).AsSkPoint(), SKTextAlign.Left, IntensityFont, paints.Foreground);
					}
				}
				return;
			case JmaIntensity.Int6Upper:
				{
					if (size < 8)
					{
						IntensityFont.Size = (float)(size * 1.25);
						canvas.DrawText("+", new PointD(leftTop.X + size * .1, leftTop.Y + size * .8).AsSkPoint(), SKTextAlign.Left, IntensityFont, paints.Foreground);
						break;
					}
					IntensityFont.Size = size;
					canvas.DrawText("6", new PointD(leftTop.X + size * (wide ? .07 : .04), leftTop.Y + size * .86).AsSkPoint(), SKTextAlign.Left, IntensityFont, paints.Foreground);
					if (wide)
					{
						IntensitySubFont.Size = (float)(size * .57);
						canvas.DrawText("強", new PointD(leftTop.X + size * .66, leftTop.Y + size * .84).AsSkPoint(), SKTextAlign.Left, IntensitySubFont, paints.Foreground);
					}
					else
					{
						IntensityFont.Size = (float)(size * .9);
						canvas.DrawText("+", new PointD(leftTop.X + size * .46, leftTop.Y + size * .64).AsSkPoint(), SKTextAlign.Left, IntensityFont, paints.Foreground);
					}
				}
				return;
			case JmaIntensity.Unknown:
				IntensityFont.Size = size;
				canvas.DrawText("-", new PointD(leftTop.X + size * (wide ? .44 : .265), leftTop.Y + size * .805).AsSkPoint(), SKTextAlign.Left, IntensityFont, paints.Foreground);
				return;
			case JmaIntensity.Error:
				IntensitySubFont.Size = size;
				canvas.DrawText("E", new PointD(leftTop.X + size * (wide ? .35 : .18), leftTop.Y + size * .88).AsSkPoint(), SKTextAlign.Left, IntensitySubFont, paints.Foreground);
				return;
		}
		if (size >= 8)
		{
			IntensityFont.Size = size;
			canvas.DrawText(intensity.ToShortString(), new PointD(leftTop.X + size * (wide ? .36 : .178), leftTop.Y + size * .87).AsSkPoint(), SKTextAlign.Left, IntensityFont, paints.Foreground);
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
		var leftTop = centerPosition ? (PointD)point - halfSize : (PointD)point;

		paints.Border.StrokeWidth = size * BorderMultiply;

		if (circle && !wide)
		{
			canvas.DrawCircle(centerPosition ? point : (SKPoint)((PointD)point + halfSize), size / 2, paints.Background);
			if (border)
				canvas.DrawCircle(centerPosition ? point : (SKPoint)((PointD)point + halfSize), size / 2, paints.Border);
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
			case LpgmIntensity.LpgmInt0:
				if (size >= 8)
				{
					IntensityFont.Size = size;
					canvas.DrawText(intensity.ToShortString(), new PointD(leftTop.X + size * (wide ? .33 : .155), leftTop.Y + size * .87).AsSkPoint(), SKTextAlign.Left, IntensityFont, paints.Foreground);
				}
				return;
			case LpgmIntensity.LpgmInt1:
				if (size >= 8)
				{
					IntensityFont.Size = size;
					canvas.DrawText(intensity.ToShortString(), new PointD(leftTop.X + size * (wide ? .42 : .25), leftTop.Y + size * .87).AsSkPoint(), SKTextAlign.Left, IntensityFont, paints.Foreground);
				}
				return;
			case LpgmIntensity.LpgmInt4:
				if (size >= 8)
				{
					IntensityFont.Size = size;
					canvas.DrawText(intensity.ToShortString(), new PointD(leftTop.X + size * (wide ? .31 : .14), leftTop.Y + size * .87).AsSkPoint(), SKTextAlign.Left, IntensityFont, paints.Foreground);
				}
				return;
			case LpgmIntensity.Unknown:
				IntensityFont.Size = size;
				canvas.DrawText("-", new PointD(leftTop.X + size * (wide ? .52 : .32), leftTop.Y + size * .8).AsSkPoint(), SKTextAlign.Left, IntensityFont, paints.Foreground);
				return;
			case LpgmIntensity.Error:
				IntensitySubFont.Size = size;
				canvas.DrawText("E", new PointD(leftTop.X + size * (wide ? .35 : .18), leftTop.Y + size * .88).AsSkPoint(), SKTextAlign.Left, IntensitySubFont, paints.Foreground);
				return;
		}
		if (size >= 8)
		{
			IntensityFont.Size = size;
			canvas.DrawText(intensity.ToShortString(), new PointD(leftTop.X + size * (wide ? .36 : .178), leftTop.Y + size * .87).AsSkPoint(), SKTextAlign.Left, IntensityFont, paints.Foreground);
		}
	}
}
