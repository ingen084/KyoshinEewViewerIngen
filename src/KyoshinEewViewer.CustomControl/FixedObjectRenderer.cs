using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Skia;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinMonitorLib;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace KyoshinEewViewer.CustomControl;

public static class FixedObjectRenderer
{
	private static readonly SKFont Font = new()
	{
		Edging = SKFontEdging.SubpixelAntialias,
		Size = 24
	};

	public const double IntensityWideScale = .75;

	public static ConcurrentDictionary<JmaIntensity, (SKPaint b, SKPaint f)> IntensityPaintCache { get; } = new();
	public static ConcurrentDictionary<LpgmIntensity, (SKPaint b, SKPaint f)> LpgmIntensityPaintCache { get; } = new();
	private static SKPaint? ForegroundPaint { get; set; }
	private static SKPaint? SubForegroundPaint { get; set; }
	private static SKPaint BorderPaint { get; } = new SKPaint
	{
		Style = SKPaintStyle.Stroke,
		Color = SKColors.Black,
		StrokeWidth = 1,
		IsAntialias = true,
	};

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

			IntensityPaintCache.AddOrUpdate(i, (b, f), (v, c) =>
			{
				c.b.Dispose();
				c.f.Dispose();
				return (b, f);
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

			LpgmIntensityPaintCache.AddOrUpdate(i, (b, f), (v, c) =>
			{
				c.b.Dispose();
				c.f.Dispose();
				return (b, f);
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
	/// <param name="round">縁を丸めるか wide,circleがfalseのときのみ有効</param>
	public static void DrawIntensity(this SKCanvas canvas, JmaIntensity intensity, SKPoint point, float size, bool centerPosition = false, bool circle = false, bool wide = false, bool round = false)
	{
		if (!IntensityPaintCache.TryGetValue(intensity, out var paints))
			return;

		var halfSize = new PointD(size / 2, size / 2);
		if (wide)
			halfSize.X /= IntensityWideScale;
		var leftTop = centerPosition ? point - halfSize : (PointD)point;

		if (circle && !wide)
			canvas.DrawCircle(centerPosition ? point : (SKPoint)(point + halfSize), size / 2, paints.b);
		else if (round && !wide)
		{
			canvas.DrawRoundRect((float)leftTop.X, (float)leftTop.Y, (float)(wide ? size / IntensityWideScale : size), size, size * .2f, size * .2f, paints.b);
			canvas.DrawRoundRect((float)leftTop.X, (float)leftTop.Y, (float)(wide ? size / IntensityWideScale : size), size, size * .2f, size * .2f, BorderPaint);
		}
		else
			canvas.DrawRect((float)leftTop.X, (float)leftTop.Y, (float)(wide ? size / IntensityWideScale : size), size, paints.b);

		switch (intensity)
		{
			case JmaIntensity.Int1:
				if (size >= 8)
				{
					paints.f.TextSize = size;
					canvas.DrawText(intensity.ToShortString(), new PointD(leftTop.X + size * (wide ? .38 : .2), leftTop.Y + size * .87).AsSkPoint(), paints.f);
				}
				return;
			case JmaIntensity.Int4:
				if (size >= 8)
				{
					paints.f.TextSize = size;
					canvas.DrawText(intensity.ToShortString(), new PointD(leftTop.X + size * (wide ? .38 : .19), leftTop.Y + size * .87).AsSkPoint(), paints.f);
				}
				return;
			case JmaIntensity.Int7:
				if (size >= 8)
				{
					paints.f.TextSize = size;
					canvas.DrawText(intensity.ToShortString(), new PointD(leftTop.X + size * (wide ? .38 : .22), leftTop.Y + size * .89).AsSkPoint(), paints.f);
				}
				return;
			case JmaIntensity.Int5Lower:
				{
					if (size < 8)
					{
						paints.f.TextSize = (float)(size * 1.25);
						canvas.DrawText("-", new PointD(leftTop.X + size * .25, leftTop.Y + size * .8).AsSkPoint(), paints.f);
						break;
					}
					paints.f.TextSize = size;
					canvas.DrawText("5", new PointD(leftTop.X + size * .1, leftTop.Y + size * .87).AsSkPoint(), paints.f);
					if (wide)
					{
						paints.f.TextSize = (float)(size * .55);
						canvas.DrawText("弱", new PointD(leftTop.X + size * .65, leftTop.Y + size * .85).AsSkPoint(), paints.f);
					}
					else
					{
						paints.f.TextSize = size;
						canvas.DrawText("-", new PointD(leftTop.X + size * .6, leftTop.Y + size * .6).AsSkPoint(), paints.f);
					}
				}
				return;
			case JmaIntensity.Int5Upper:
				{
					if (size < 8)
					{
						paints.f.TextSize = (float)(size * 1.25);
						canvas.DrawText("+", new PointD(leftTop.X + size * .1, leftTop.Y + size * .8).AsSkPoint(), paints.f);
						break;
					}
					paints.f.TextSize = size;
					canvas.DrawText("5", new PointD(leftTop.X + size * .1, leftTop.Y + size * .87).AsSkPoint(), paints.f);
					if (wide)
					{
						paints.f.TextSize = (float)(size * .55);
						canvas.DrawText("強", new PointD(leftTop.X + size * .65, leftTop.Y + size * .85).AsSkPoint(), paints.f);
					}
					else
					{
						paints.f.TextSize = (float)(size * .8);
						canvas.DrawText("+", new PointD(leftTop.X + size * .5, leftTop.Y + size * .65).AsSkPoint(), paints.f);
					}
				}
				return;
			case JmaIntensity.Int6Lower:
				{
					if (size < 8)
					{
						paints.f.TextSize = (float)(size * 1.25);
						canvas.DrawText("-", new PointD(leftTop.X + size * .25, leftTop.Y + size * .8).AsSkPoint(), paints.f);
						break;
					}
					paints.f.TextSize = size;
					canvas.DrawText("6", new PointD(leftTop.X + size * .1, leftTop.Y + size * .86).AsSkPoint(), paints.f);
					if (wide)
					{
						paints.f.TextSize = (float)(size * .55);
						canvas.DrawText("弱", new PointD(leftTop.X + size * .65, leftTop.Y + size * .85).AsSkPoint(), paints.f);
					}
					else
					{
						paints.f.TextSize = size;
						canvas.DrawText("-", new PointD(leftTop.X + size * .6, leftTop.Y + size * .6).AsSkPoint(), paints.f);
					}
				}
				return;
			case JmaIntensity.Int6Upper:
				{
					if (size < 8)
					{
						paints.f.TextSize = (float)(size * 1.25);
						canvas.DrawText("+", new PointD(leftTop.X + size * .1, leftTop.Y + size * .8).AsSkPoint(), paints.f);
						break;
					}
					paints.f.TextSize = size;
					canvas.DrawText("6", new PointD(leftTop.X + size * .1, leftTop.Y + size * .86).AsSkPoint(), paints.f);
					if (wide)
					{
						paints.f.TextSize = (float)(size * .55);
						canvas.DrawText("強", new PointD(leftTop.X + size * .65, leftTop.Y + size * .85).AsSkPoint(), paints.f);
					}
					else
					{
						paints.f.TextSize = (float)(size * .8);
						canvas.DrawText("+", new PointD(leftTop.X + size * .5, leftTop.Y + size * .65).AsSkPoint(), paints.f);
					}
				}
				return;
			case JmaIntensity.Unknown:
				paints.f.TextSize = size;
				canvas.DrawText("-", new PointD(leftTop.X + size * (wide ? .52 : .32), leftTop.Y + size * .8).AsSkPoint(), paints.f);
				return;
			case JmaIntensity.Error:
				paints.f.TextSize = size;
				canvas.DrawText("E", new PointD(leftTop.X + size * (wide ? .35 : .18), leftTop.Y + size * .88).AsSkPoint(), paints.f);
				return;
		}
		if (size >= 8)
		{
			paints.f.TextSize = size;
			canvas.DrawText(intensity.ToShortString(), new PointD(leftTop.X + size * (wide ? .38 : .22), leftTop.Y + size * .87).AsSkPoint(), paints.f);
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
	/// <param name="round">縁を丸めるか wide,circleがfalseのときのみ有効</param>
	public static void DrawLpgmIntensity(this SKCanvas canvas, LpgmIntensity intensity, SKPoint point, float size, bool centerPosition = false, bool circle = false, bool wide = false, bool round = false)
	{
		if (!LpgmIntensityPaintCache.TryGetValue(intensity, out var paints))
			return;

		var halfSize = new PointD(size / 2, size / 2);
		if (wide)
			halfSize.X /= IntensityWideScale;
		var leftTop = centerPosition ? point - halfSize : (PointD)point;

		if (circle && !wide)
			canvas.DrawCircle(centerPosition ? point : (SKPoint)(point + halfSize), size / 2, paints.b);
		else if (round && !wide)
			canvas.DrawRoundRect((float)leftTop.X, (float)leftTop.Y, (float)(wide ? size / IntensityWideScale : size), size, size * .2f, size * .2f, paints.b);
		else
			canvas.DrawRect((float)leftTop.X, (float)leftTop.Y, (float)(wide ? size / IntensityWideScale : size), size, paints.b);

		switch (intensity)
		{
			case LpgmIntensity.LpgmInt1:
				if (size >= 8)
				{
					paints.f.TextSize = size;
					canvas.DrawText(intensity.ToShortString(), new PointD(leftTop.X + size * (wide ? .38 : .2), leftTop.Y + size * .87).AsSkPoint(), paints.f);
				}
				return;
			case LpgmIntensity.LpgmInt4:
				if (size >= 8)
				{
					paints.f.TextSize = size;
					canvas.DrawText(intensity.ToShortString(), new PointD(leftTop.X + size * (wide ? .38 : .19), leftTop.Y + size * .87).AsSkPoint(), paints.f);
				}
				return;
			case LpgmIntensity.Unknown:
				paints.f.TextSize = size;
				canvas.DrawText("-", new PointD(leftTop.X + size * (wide ? .52 : .32), leftTop.Y + size * .8).AsSkPoint(), paints.f);
				return;
			case LpgmIntensity.Error:
				paints.f.TextSize = size;
				canvas.DrawText("E", new PointD(leftTop.X + size * (wide ? .35 : .18), leftTop.Y + size * .88).AsSkPoint(), paints.f);
				return;
		}
		if (size >= 8)
		{
			paints.f.TextSize = size;
			canvas.DrawText(intensity.ToShortString(), new PointD(leftTop.X + size * (wide ? .38 : .22), leftTop.Y + size * .87).AsSkPoint(), paints.f);
		}
	}

	public static void DrawLinkedRealtimeData(this SKCanvas canvas, IEnumerable<RealtimeObservationPoint>? points, float height, float maxWidth, float maxHeight, RealtimeDataRenderMode mode)
	{
		if (points == null || ForegroundPaint == null || SubForegroundPaint == null) return;

		var count = 0;
		var verticalOffset = 0f;
		foreach (var point in points)
		{
			if (point.IsTmpDisabled)
				continue;

			var horizontalOffset = 0f;
			switch (mode)
			{
				case RealtimeDataRenderMode.ShindoIconAndRawColor:
					if (point.LatestIntensity.ToJmaIntensity() >= JmaIntensity.Int1)
						goto case RealtimeDataRenderMode.ShindoIcon;
					goto case RealtimeDataRenderMode.RawColor;
				case RealtimeDataRenderMode.ShindoIconAndMonoColor:
					if (point.LatestIntensity.ToJmaIntensity() >= JmaIntensity.Int1)
						goto case RealtimeDataRenderMode.ShindoIcon;
					{
						if (point.LatestColor is { } color)
						{
							var num = (byte)(color.Red / 3 + color.Green / 3 + color.Blue / 3);
							using var rectPaint = new SKPaint
							{
								Style = SKPaintStyle.Fill,
								Color = new SKColor(num, num, num),
							};
							canvas.DrawRect(0, verticalOffset, height / 5, height, rectPaint);
						}
						horizontalOffset += height / 5;
					}
					break;
				case RealtimeDataRenderMode.ShindoIcon:
					canvas.DrawIntensity(point.LatestIntensity.ToJmaIntensity(), new SKPoint(0, verticalOffset), height);
					horizontalOffset += height;
					break;
				case RealtimeDataRenderMode.WideShindoIcon:
					canvas.DrawIntensity(point.LatestIntensity.ToJmaIntensity(), new SKPoint(0, verticalOffset), height, wide: true);
					horizontalOffset += height * 1.25f;
					break;
				case RealtimeDataRenderMode.RawColor:
					{
						if (point.LatestColor is { } color)
						{
							using var rectPaint = new SKPaint
							{
								Style = SKPaintStyle.Fill,
								Color = color,
							};
							canvas.DrawRect(0, verticalOffset, height / 5, height, rectPaint);
						}
						horizontalOffset += height / 5;
					}
					break;
			}

			var region = point.Region;
			if (region.Length > 3)
				region = region[..3];

#if DEBUG
			var prevColor = ForegroundPaint.Color;
			if (point.Event != null)
				ForegroundPaint.Color = point.Event.DebugColor;
#endif

			Font.Size = height * .6f;
			Font.Typeface = KyoshinEewViewerFonts.MainRegular;
			canvas.DrawText(region, horizontalOffset + height * 0.1f, verticalOffset + height * .9f, Font, ForegroundPaint);
			horizontalOffset += Math.Max(ForegroundPaint.MeasureText(region), maxWidth / 4);

			Font.Size = height * .75f;
			Font.Typeface = KyoshinEewViewerFonts.MainBold;
			canvas.DrawText(point.Name, horizontalOffset, verticalOffset + height * .9f, Font, ForegroundPaint);

			Font.Size = height * .6f;
			Font.Typeface = KyoshinEewViewerFonts.MainRegular;
			SubForegroundPaint.TextAlign = SKTextAlign.Right;
			canvas.DrawText(point.LatestIntensity?.ToString("0.0") ?? "?", maxWidth, verticalOffset + height, Font, SubForegroundPaint);
			SubForegroundPaint.TextAlign = SKTextAlign.Left;

#if DEBUG
			ForegroundPaint.Color = prevColor;
#endif

			count++;
			verticalOffset += height;
			if (verticalOffset >= maxHeight)
				return;
		}
	}
}
