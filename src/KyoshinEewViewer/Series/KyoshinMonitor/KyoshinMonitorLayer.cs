using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Skia;
using KyoshinEewViewer.Core;
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
using System.Collections.Generic;
using System.Linq;
using Location = KyoshinMonitorLib.Location;

namespace KyoshinEewViewer.Series.KyoshinMonitor;

public class KyoshinMonitorLayer(KyoshinMonitorWatchService watcher, KyoshinEewViewerConfiguration config, TimerService timerService) : MapLayer
{
	private RealtimeObservationPoint[]? _observationPoints;
	public RealtimeObservationPoint[]? ObservationPoints
	{
		get => _observationPoints;
		set {
			_observationPoints = value;
			RefreshRequest();
		}
	}

	private KyoshinEvent[]? _kyoshinEvents;
	public KyoshinEvent[]? KyoshinEvents
	{
		get => _kyoshinEvents;
		set {
			_kyoshinEvents = value;
			RefreshRequest();
		}
	}

	private IEew[]? _currentEews;
	public IEew[]? CurrentEews
	{
		get => _currentEews;
		set {
			_currentEews = value;
			RefreshRequest();
		}
	}

	private Location? _currentLocation;
	public Location? CurrentLocation
	{
		get => _currentLocation;
		set {
			_currentLocation = value;
			RefreshRequest();
		}
	}


	private static readonly SKPaint TextPaint = new()
	{
		Typeface = KyoshinEewViewerFonts.MainRegular,
		TextSize = 14,
		StrokeWidth = 2,
		IsAntialias = true,
		SubpixelText = true,
		LcdRenderText = true,
	};
	private static readonly SKPaint TextBackgroundPaint = new()
	{
		IsAntialias = true,
		Color = SKColors.Gray,
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
		StrokeWidth = 2,
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
	private SKPaint HypocenterBorderPen { get; } = new SKPaint
	{
		Style = SKPaintStyle.Stroke,
		Color = new SKColor(255, 255, 0, 255),
		StrokeWidth = 6,
		IsAntialias = true,
	};
	private SKPaint HypocenterPen { get; } = new SKPaint
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

	public override bool NeedPersistentUpdate => !Config.Eew.DisableAnimation && (CurrentEews?.Length ?? 0) > 0;
	private bool IsDarkTheme { get; set; }

	private SKColor ForecastHypocenterBorder { get; set; }
	private SKColor ForecastHypocenter { get; set; }
	private SKColor ForecastPWave { get; set; }
	private SKColor ForecastSWave { get; set; }
	private bool IsForecastSWaveGradient { get; set; }
	private SKColor WarningHypocenterBorder { get; set; }
	private SKColor WarningHypocenter { get; set; }
	private SKColor WarningPWave { get; set; }
	private SKColor WarningSWave { get; set; }
	private bool IsWarningSWaveGradient { get; set; }
	private bool IsHypocenterBlinkAnimation { get; set; }

	private KyoshinMonitorWatchService Watcher { get; } = watcher;
	private KyoshinEewViewerConfiguration Config { get; } = config;
	private TimerService TimerService { get; } = timerService;

	public override void RefreshResourceCache(Control targetControl)
	{
		bool FindBoolResource(string name)
			=> (bool)(targetControl.FindResource(name) ?? throw new Exception($"リソース {name} が見つかりませんでした"));
		SKColor FindColorResource(string name)
			=> ((Color)(targetControl.FindResource(name) ?? throw new Exception($"リソース {name} が見つかりませんでした"))).ToSKColor();

		IsDarkTheme = FindBoolResource("IsDarkTheme");

		ForecastHypocenterBorder = FindColorResource("EewForecastHypocenterBorderColor");
		ForecastHypocenter = FindColorResource("EewForecastHypocenterColor");
		ForecastPWave = FindColorResource("EewForecastPWaveColor");
		ForecastSWave = FindColorResource("EewForecastSWaveColor");
		IsForecastSWaveGradient = FindBoolResource("IsEewForecastSWaveGradient");

		WarningHypocenterBorder = FindColorResource("EewWarningHypocenterBorderColor");
		WarningHypocenter = FindColorResource("EewWarningHypocenterColor");
		WarningPWave = FindColorResource("EewWarningPWaveColor");
		WarningSWave = FindColorResource("EewWarningSWaveColor");
		IsWarningSWaveGradient = FindBoolResource("IsEewWarningSWaveGradient");

		IsHypocenterBlinkAnimation = FindBoolResource("IsEewHypocenterBlinkAnimation");
	}

	public override void Render(SKCanvas canvas, LayerRenderParameter param, bool isAnimating)
	{
		canvas.Save();
		try
		{
			var zoom = param.Zoom;
			canvas.Translate((float)-param.LeftTopPixel.X, (float)-param.LeftTopPixel.Y);

			var pixelBound = param.PixelBound;

			RenderObservationPoints();
			void RenderObservationPoints()
			{
				if (ObservationPoints == null)
					return;

				var renderedPoints = new List<RealtimeObservationPoint>();
				var fixedRect = new List<RectD>();

				// 描画対象の観測点のリストアップ
				foreach (var point in ObservationPoints)
				{
					// 設定以下の震度であれば描画しない
					if (point.LatestIntensity != null && point.LatestIntensity < Config.RawIntensityObject.MinShownIntensity)
						continue;

					var circleSize = (float)(Math.Max(1, zoom - 4) * 1.75);
					var circleVector = new PointD(circleSize, circleSize);
					var pointCenter = point.Location.ToPixel(zoom);
					var bound = new RectD(pointCenter - circleVector, pointCenter + circleVector);
					if (!pixelBound.IntersectsWith(bound))
						continue;

					// 観測震度が取得できず、過去に観測履歴が存在し、設定で観測できない地点の描画設定が有効であれば描画対象として登録する
					if (point.LatestIntensity == null && (
						!Config.RawIntensityObject.ShowInvalidateIcon ||
						!point.HasValidHistory))
						continue;

					// 異常値除外
					if (!Config.RawIntensityObject.ShowInvalidateIcon && point.IsTmpDisabled)
						continue;

					renderedPoints.Add(point);
					fixedRect.Add(bound);
				}

				var ordersRenderedPoints = renderedPoints.OrderByDescending(p => p.LatestIntensity ?? -1000);
//				var isTextRenderLevel = zoom >= Config.RawIntensityObject.ShowNameZoomLevel || zoom >= Config.RawIntensityObject.ShowValueZoomLevel;
//				// 観測点名の描画
//				if (isTextRenderLevel)
//					foreach (var point in ordersRenderedPoints)
//					{
//						if (point.LatestIntensity is null && !point.HasValidHistory && Config.RawIntensityObject.ShowInvalidateIcon)
//							continue;
//						if (point.LatestIntensity < Config.RawIntensityObject.MinShownDetailIntensity)
//							continue;

//						var rawIntensity = point.LatestIntensity ?? 0;
//						var intensity = Math.Clamp(rawIntensity, -3, 7);
//						var circleSize = Math.Max(1, zoom - 4) * 1.75;
//						var origCenterPoint = point.Location.ToPixel(zoom) + new PointD(circleSize + 2, TextPaint.TextSize * .4);
//						var centerPoint = origCenterPoint;

//						var text =
//#if DEBUG
//							point.IntensityDiff.ToString("+0.0;-0.0") + " " +
//#endif
//							(zoom >= Config.RawIntensityObject.ShowNameZoomLevel ? point.Name + " " : "") +
//							(zoom >= Config.RawIntensityObject.ShowValueZoomLevel ? (point.LatestIntensity == null ? "-" : intensity.ToString("0.0")) : "");

//						if (point.IsTmpDisabled)
//							text = "(異常値)" + text;

//						var textWidth = TextPaint.MeasureText(text);

//						// デフォルトでは右側に
//						var origBound = new RectD(
//							centerPoint - new PointD(0, TextPaint.TextSize * .7),
//							centerPoint + new PointD(textWidth, TextPaint.TextSize * .1));
//						var bound = origBound;
//						var linkOrigin = origBound.BottomLeft + new PointD(1, 1);

//						// 文字の被りチェック
//						if (fixedRect.Any(r => r.IntersectsWith(bound)))
//						{
//							// 左側での描画を試す
//							var diffV = new PointD(bound.Width + (circleSize + 2) * 2, 0);
//							bound = new RectD(bound.TopLeft - diffV, bound.BottomRight - diffV);
//							centerPoint -= diffV;
//							linkOrigin = bound.BottomRight + new PointD(-1, 1);

//							if (fixedRect.Any(r => r.IntersectsWith(bound)))
//							{
//								// 上側での描画を試す
//								var diffV2 = new PointD(bound.Width / 2 + (circleSize + 2), (circleSize + 2) * 2);
//								bound = new RectD(origBound.TopLeft - diffV2, origBound.BottomRight - diffV2);
//								centerPoint = origCenterPoint - diffV2;
//								linkOrigin = bound.BottomRight - new PointD(bound.Width / 2, -1);

//								if (fixedRect.Any(r => r.IntersectsWith(bound)))
//								{
//									// 下側での描画を試す
//									var diffV3 = new PointD(bound.Width / 2 + (circleSize + 2), -((circleSize + 1) * 2));
//									bound = new RectD(origBound.TopLeft - diffV3, origBound.BottomRight - diffV3);
//									centerPoint = origCenterPoint - diffV3;
//									linkOrigin = bound.BottomRight - new PointD(bound.Width / 2, 1);

//									if (fixedRect.Any(r => r.IntersectsWith(bound)))
//										continue;
//								}
//							}
//						}
//						fixedRect.Add(bound);

//						TextBackgroundPaint.Color = point.LatestColor ?? SKColors.Gray;
//						canvas.DrawLine(linkOrigin.AsSkPoint(), point.Location.ToPixel(zoom).AsSkPoint(), TextBackgroundPaint);

//						canvas.DrawRect(
//							(float)bound.Left,
//							(float)(bound.Top + bound.Height),
//							(float)bound.Width,
//							2,
//							TextBackgroundPaint);

//						var loc = (centerPoint + new PointD(1, 0)).AsSkPoint();
//						TextPaint.Style = SKPaintStyle.Stroke;
//						TextPaint.Color = IsDarkTheme ? SKColors.Black : SKColors.White;
//						canvas.DrawText(text, loc, TextPaint);
//						TextPaint.Style = SKPaintStyle.Fill;
//						TextPaint.Color = IsDarkTheme ? SKColors.White : SKColors.Black;
//						canvas.DrawText(text, loc, TextPaint);
//					}

				// 観測点本体の描画
				foreach (var point in ordersRenderedPoints.Reverse())
				{
					// 描画しない
					if (point.LatestIntensity != null && point.LatestIntensity < Config.RawIntensityObject.MinShownIntensity)
						continue;

					var circleSize = (float)(Math.Max(1, zoom - 4) * 1.75);
					var circleVector = new PointD(circleSize, circleSize);
					var pointCenter = point.Location.ToPixel(zoom);
					var bound = new RectD(pointCenter - circleVector, pointCenter + circleVector);
					if (!pixelBound.IntersectsWith(bound))
						continue;

					var color = point.LatestColor;

					// 震度アイコンの描画
					//if (Config.RawIntensityObject.ShowIntensityIcon && !point.IsTmpDisabled && point.LatestIntensity is not null && color is not null)
					//{
					//	if (point.LatestIntensity >= 0.5)
					//	{
					//		FixedObjectRenderer.DrawIntensity(
					//			canvas,
					//			JmaIntensityExtensions.ToJmaIntensity(point.LatestIntensity),
					//			pointCenter.AsSkPoint(),
					//			circleSize * 2,
					//			true,
					//			true);
					//		continue;
					//	}
					//	// 震度1未満であればモノクロに
					//	var num = (byte)(color.Value.Red / 3 + color.Value.Green / 3 + color.Value.Blue / 3);
					//	color = new SKColor(num, num, num);
					//}
					// 無効な観測点
					if (point.LatestIntensity == null || point.IsTmpDisabled)
					{
						// の描画
						if (Config.RawIntensityObject.ShowInvalidateIcon)
						{
							if (point.IsTmpDisabled)
								InvalidatePaint.Color = point.LatestColor ?? SKColors.Gray;
							canvas.DrawCircle(
								pointCenter.AsSkPoint(),
								circleSize,
								InvalidatePaint);
							if (point.IsTmpDisabled)
								InvalidatePaint.Color = SKColors.Gray;
						}
						continue;
					}

					if (color is not null)
					{
						PointPaint.Color = color.Value;
						// 観測点の色
						canvas.DrawCircle(
							pointCenter.AsSkPoint(),
							circleSize,
							PointPaint);
					}

#if DEBUG
					if (point.Event != null)
					{
						PointPaint.Color = TextPaint.Color = point.Event.DebugColor;
						TextPaint.Style = SKPaintStyle.Stroke;
						canvas.DrawCircle(
							pointCenter.AsSkPoint(),
							circleSize / 2,
							PointPaint);
						var tgnp = point.NearPoints?.Where(np => np.IntensityDiff >= .5);
						if (tgnp != null && tgnp.Any())
							foreach (var np in tgnp)
								if (np.Event == null)
									canvas.DrawLine(pointCenter.AsSkPoint(), np.Location.ToPixel(zoom).AsSkPoint(), TextPaint);
					}
#endif
				}
			}

			RenderEews();
			void RenderEews()
			{
				if (CurrentEews == null)
					return;

				foreach (var eew in CurrentEews)
				{
					if (eew.Location == null)
						continue;

					// 震央
					var minSize = 8 + (zoom - 5) * 1.25;
					var maxSize = minSize + 1;

					var basePoint = eew.Location.ToPixel(zoom);


					//   0 ~ 500 : 255 ~ 55
					// 501 ~ 999 : 55 ~ 255
					var ms = TimerService.CurrentTime.Millisecond;
					if (ms > 500)
						ms = 1000 - ms;
					if (IsHypocenterBlinkAnimation && !Config.Eew.DisableAnimation)
					{
						if (eew.IsWarning)
						{
							HypocenterBorderPen.Color = WarningHypocenterBorder.WithAlpha((byte)(55 + (ms / 500.0 * 200)));
							HypocenterPen.Color = WarningHypocenter.WithAlpha((byte)(55 + (ms / 500.0 * 200)));
						}
						else
						{
							HypocenterBorderPen.Color = ForecastHypocenterBorder.WithAlpha((byte)(55 + (ms / 500.0 * 200)));
							HypocenterPen.Color = ForecastHypocenter.WithAlpha((byte)(55 + (ms / 500.0 * 200)));
						}
					}
					else
					{
						if (eew.IsWarning)
						{
							HypocenterBorderPen.Color = WarningHypocenterBorder;
							HypocenterPen.Color = WarningHypocenter;
						}
						else
						{
							HypocenterBorderPen.Color = ForecastHypocenterBorder;
							HypocenterPen.Color = ForecastHypocenter;
						}
					}
					if (IsHypocenterBlinkAnimation || TimerService.CurrentTime.Millisecond < 500 || !Config.Eew.DisableAnimation)
					{
						// 仮定震源要素もしくは精度が保証されていないときは円を表示させる
						if (eew.IsTemporaryEpicenter || eew.LocationAccuracy == 1)
						{
							canvas.DrawCircle(basePoint.AsSkPoint(), (float)maxSize, HypocenterBorderPen);
							canvas.DrawCircle(basePoint.AsSkPoint(), (float)minSize, HypocenterPen);
						}
						else
						{
							canvas.DrawLine((basePoint - new PointD(maxSize, maxSize)).AsSkPoint(), (basePoint + new PointD(maxSize, maxSize)).AsSkPoint(), HypocenterBorderPen);
							canvas.DrawLine((basePoint - new PointD(-maxSize, maxSize)).AsSkPoint(), (basePoint + new PointD(-maxSize, maxSize)).AsSkPoint(), HypocenterBorderPen);
							canvas.DrawLine((basePoint - new PointD(minSize, minSize)).AsSkPoint(), (basePoint + new PointD(minSize, minSize)).AsSkPoint(), HypocenterPen);
							canvas.DrawLine((basePoint - new PointD(-minSize, minSize)).AsSkPoint(), (basePoint + new PointD(-minSize, minSize)).AsSkPoint(), HypocenterPen);
						}
					}

					// P/S波 仮定震源要素でなく、位置と精度が保証されているときのみ表示する
					if (!eew.IsTemporaryEpicenter && eew.LocationAccuracy != 1 && eew.DepthAccuracy != 1)
					{
						// 揺れの広がりを計算する
						// リプレイ中もしくは強震モニタの時刻をベースに表示するオプションが有効になっているときは強震モニタ側のタイマーを使用する
						(var p, var s) = TravelTimeTableService.CalcDistance(
							eew.OccurrenceTime,
							Watcher.OverrideDateTime != null || Config.Eew.SyncKyoshinMonitorPsWave || Config.Timer.TimeshiftSeconds < 0
								? Watcher.CurrentDisplayTime : TimerService.CurrentTime,
							eew.Depth);

						if (eew.IsWarning)
						{
							PWavePaint.Color = WarningPWave;
							SWavePaint.Color = WarningSWave;
						}
						else
						{
							PWavePaint.Color = ForecastPWave;
							SWavePaint.Color = ForecastSWave;
						}

						if (p is { } pDistance && pDistance > 0)
						{
							using var circle = PathGenerator.MakeCirclePath(eew.Location, pDistance * 1000, param.Zoom);
							canvas.DrawPath(circle, PWavePaint);
						}

						if (s is { } sDistance && sDistance > 0)
						{
							using var circle = PathGenerator.MakeCirclePath(eew.Location, sDistance * 1000, param.Zoom);
							if (eew.IsWarning ? IsWarningSWaveGradient : IsForecastSWaveGradient)
							{
								using var sgradPaint = new SKPaint
								{
									IsAntialias = true,
									Style = SKPaintStyle.Fill,
									Shader = SKShader.CreateRadialGradient(
											basePoint.AsSkPoint(),
											circle.Bounds.Height / 2,
										new[] { SWavePaint.Color.WithAlpha(15), SWavePaint.Color.WithAlpha(80) },
										new[] { .6f, 1f },
										SKShaderTileMode.Clamp
									)
								};
								canvas.DrawPath(circle, sgradPaint);
							}
							canvas.DrawPath(circle, SWavePaint);
						}
					}
				}
			}

			if (CurrentLocation != null)
			{
				var size = 5;

				var basePoint = CurrentLocation.ToPixel(param.Zoom);

				canvas.DrawLine((basePoint - new PointD(0, size)).AsSkPoint(), (basePoint + new PointD(0, size)).AsSkPoint(), CurrentLocationPen);
				canvas.DrawLine((basePoint - new PointD(size, 0)).AsSkPoint(), (basePoint + new PointD(size, 0)).AsSkPoint(), CurrentLocationPen);
			}

#if DEBUG
			if (KyoshinEvents != null)
				foreach (var evt in KyoshinEvents)
				{
					TextPaint.Color = evt.DebugColor;
					TextPaint.Style = SKPaintStyle.Stroke;
					var tl = evt.TopLeft.ToPixel(zoom).AsSkPoint();
					var br = evt.BottomRight.ToPixel(zoom).AsSkPoint() - tl;
					canvas.DrawRect(tl.X, tl.Y, br.X, br.Y, TextPaint);
				}
#endif
		}
		finally
		{
			canvas.Restore();
		}
	}
}
