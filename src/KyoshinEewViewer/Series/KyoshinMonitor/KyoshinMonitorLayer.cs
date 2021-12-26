using Avalonia.Controls;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Layers;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Services;
using KyoshinEewViewer.Services;
using KyoshinMonitorLib;
using SkiaSharp;
using System;
using Location = KyoshinMonitorLib.Location;

namespace KyoshinEewViewer.Series.KyoshinMonitor;

public class KyoshinMonitorLayer : MapLayer
{
	private RealtimeObservationPoint[]? observationPoints;
	public RealtimeObservationPoint[]? ObservationPoints
	{
		get => observationPoints;
		set {
			observationPoints = value;
			RefleshRequest();
		}
	}

	private Eew[]? currentEews;
	public Eew[]? CurrentEews
	{
		get => currentEews;
		set {
			currentEews = value;
			RefleshRequest();
		}
	}

	private Location? currentLocation;
	public Location? CurrentLocation
	{
		get => currentLocation;
		set {
			currentLocation = value;
			RefleshRequest();
		}
	}


	private static readonly SKPaint TextPaint = new()
	{
		IsAntialias = true,
		Typeface = FixedObjectRenderer.MainTypeface,
		TextSize = 14,
		StrokeWidth = 2,
	};
	private static readonly SKPaint InvalidatePaint = new()
	{
		Style = SKPaintStyle.Stroke,
		IsAntialias = true,
		Color = SKColors.Gray,
		StrokeWidth = 1,
	};
	private static readonly SKPaint PointPaint = new()
	{
		Style = SKPaintStyle.Fill,
		IsAntialias = true,
		StrokeWidth = 1,
	};
	private static readonly SKPaint PWavePaint = new()
	{
		IsAntialias = true,
		Style = SKPaintStyle.Stroke,
		StrokeWidth = 1,
		Color = new SKColor(0, 160, 255, 200),
	};
	private static readonly SKPaint SWavePaint = new()
	{
		IsAntialias = true,
		Style = SKPaintStyle.Stroke,
		StrokeWidth = 1,
		Color = new SKColor(255, 80, 120),
	};
	private SKPaint EpicenterBorderPen { get; } = new SKPaint
	{
		Style = SKPaintStyle.Stroke,
		Color = new SKColor(255, 255, 0, 255),
		StrokeWidth = 6,
		IsAntialias = true,
	};
	private SKPaint EpicenterPen { get; } = new SKPaint
	{
		Style = SKPaintStyle.Stroke,
		Color = new SKColor(255, 0, 0, 255),
		StrokeWidth = 4,
		IsAntialias = true,
	};
	private SKPaint CurrentLocationPen { get; } = new SKPaint
	{
		Style = SKPaintStyle.Fill,
		Color = SKColors.Magenta,
		StrokeWidth = 2,
		IsAntialias = true,
	};

	public override bool NeedPersistentUpdate => (CurrentEews?.Length ?? 0) > 0;

	private bool IsDarkTheme { get; set; }

	public override void RefreshResourceCache(Control targetControl)
	{
		bool FindBoolResource(string name)
			=> (bool)(targetControl.FindResource(name) ?? throw new Exception($"リソース {name} が見つかりませんでした"));
		IsDarkTheme = FindBoolResource("IsDarkTheme");
	}

	public override void Render(SKCanvas canvas, bool isAnimating)
	{
		if (ObservationPoints != null)
			foreach (var point in ObservationPoints)
			{
				if (point.LatestIntensity is not double shindo)
					continue;
				var intensity = Math.Clamp(shindo, -3, 7);

				// 描画しない
				if (intensity < ConfigurationService.Current.RawIntensityObject.MinShownIntensity)
					continue;

				var circleSize = (float)(Math.Max(1, Zoom - 4) * 1.75);
				var circleVector = new PointD(circleSize, circleSize);
				var pointCenter = point.Location.ToPixel(Zoom);
				if (!PixelBound.IntersectsWith(new RectD(pointCenter - circleVector, pointCenter + circleVector)))
					continue;

				// 観測点情報文字の描画
				if (!isAnimating &&
					(Zoom >= ConfigurationService.Current.RawIntensityObject.ShowNameZoomLevel || Zoom >= ConfigurationService.Current.RawIntensityObject.ShowValueZoomLevel) &&
					(!double.IsNaN(intensity) || ConfigurationService.Current.RawIntensityObject.ShowInvalidateIcon))
				{
					TextPaint.TextSize = 14;// Math.Min(circleSize * 2, 14);
					var text = 
						(Zoom >= ConfigurationService.Current.RawIntensityObject.ShowNameZoomLevel ? point.Name : "") + " " +
						(Zoom >= ConfigurationService.Current.RawIntensityObject.ShowValueZoomLevel ? (double.IsNaN(intensity) ? "-" : intensity.ToString("0.0")) : "");
					var loc = (pointCenter - LeftTopPixel + new PointD(circleSize, TextPaint.TextSize * .4)).AsSKPoint();

					TextPaint.Style = SKPaintStyle.Stroke;
					TextPaint.Color = !IsDarkTheme ? SKColors.White : SKColors.Black;
					canvas.DrawText(text, loc, TextPaint);
					TextPaint.Style = SKPaintStyle.Fill;
					TextPaint.Color = IsDarkTheme ? SKColors.White : SKColors.Black;
					canvas.DrawText(text, loc, TextPaint);
				}

				var color = point.LatestColor;

				// 震度アイコンの描画
				if (ConfigurationService.Current.RawIntensityObject.ShowIntensityIcon && color is SKColor)
				{
					if (intensity >= 0.5)
					{
						FixedObjectRenderer.DrawIntensity(
							canvas,
							JmaIntensityExtensions.ToJmaIntensity(point.LatestIntensity),
							(SKPoint)(pointCenter - LeftTopPixel),
							circleSize * 2,
							true,
							true);
						continue;
					}
					// 震度1未満であればモノクロに
					var num = (byte)(color.Value.Red / 3 + color.Value.Green / 3 + color.Value.Blue / 3);
					color = new SKColor(num, num, num);
				}
				// 無効な観測点
				if (double.IsNaN(intensity))
				{
					// の描画
					if (ConfigurationService.Current.RawIntensityObject.ShowInvalidateIcon)
					{
						canvas.DrawCircle(
							(pointCenter - LeftTopPixel).AsSKPoint(),
							circleSize,
							InvalidatePaint);
					}
					continue;
				}

				if (color is SKColor)
				{
					PointPaint.Color = color.Value;
					// 観測点の色
					canvas.DrawCircle(
						(pointCenter - LeftTopPixel).AsSKPoint(),
						circleSize,
						PointPaint);
				}
			}

		if (CurrentEews != null)
		{
			canvas.Save();
			canvas.Translate((float)-LeftTopPixel.X, (float)-LeftTopPixel.Y);

			foreach (var eew in CurrentEews)
			{
				if (eew.Location == null)
					continue;

				// 震央
				var minSize = 8 + (Zoom - 5) * 1.25;
				var maxSize = minSize + 1;

				var basePoint = eew.Location.ToPixel(Zoom);
				if (eew.IsUnreliableLocation)
				{
					canvas.DrawCircle(basePoint.AsSKPoint(), (float)maxSize, EpicenterBorderPen);
					canvas.DrawCircle(basePoint.AsSKPoint(), (float)minSize, EpicenterPen);
				}
				else
				{
					canvas.DrawLine((basePoint - new PointD(maxSize, maxSize)).AsSKPoint(), (basePoint + new PointD(maxSize, maxSize)).AsSKPoint(), EpicenterBorderPen);
					canvas.DrawLine((basePoint - new PointD(-maxSize, maxSize)).AsSKPoint(), (basePoint + new PointD(-maxSize, maxSize)).AsSKPoint(), EpicenterBorderPen);
					canvas.DrawLine((basePoint - new PointD(minSize, minSize)).AsSKPoint(), (basePoint + new PointD(minSize, minSize)).AsSKPoint(), EpicenterPen);
					canvas.DrawLine((basePoint - new PointD(-minSize, minSize)).AsSKPoint(), (basePoint + new PointD(-minSize, minSize)).AsSKPoint(), EpicenterPen);
				}

				// P/S波
				(var p, var s) = TravelTimeTableService.CalcDistance(eew.OccurrenceTime, TimerService.Default.CurrentDisplayTime, eew.Depth);

				if (p is double pDistance && pDistance > 0)
				{
					using var circle = PathGenerator.MakeCirclePath(eew.Location, pDistance * 1000, Zoom);
					canvas.DrawPath(circle, PWavePaint);
				}

				if (s is double sDistance && sDistance > 0)
				{
					using var circle = PathGenerator.MakeCirclePath(eew.Location, sDistance * 1000, Zoom);
					using var sgradPaint = new SKPaint
					{
						IsAntialias = true,
						Style = SKPaintStyle.Fill,
						Shader = SKShader.CreateRadialGradient(
								basePoint.AsSKPoint(),
								circle.Bounds.Height / 2,
							new[] { new SKColor(255, 80, 120, 15), new SKColor(255, 80, 120, 80) },
							new[] { .6f, 1f },
							SKShaderTileMode.Clamp
						)
					};
					canvas.DrawPath(circle, sgradPaint);
					canvas.DrawPath(circle, SWavePaint);
				}
			}

			canvas.Restore();
		}

		if (CurrentLocation != null)
		{
			var size = 5;

			var basePoint = CurrentLocation.ToPixel(Zoom) - LeftTopPixel;

			canvas.DrawLine((basePoint - new PointD(0, size)).AsSKPoint(), (basePoint + new PointD(0, size)).AsSKPoint(), CurrentLocationPen);
			canvas.DrawLine((basePoint - new PointD(size, 0)).AsSKPoint(), (basePoint + new PointD(size, 0)).AsSKPoint(), CurrentLocationPen);
		}
	}
}
