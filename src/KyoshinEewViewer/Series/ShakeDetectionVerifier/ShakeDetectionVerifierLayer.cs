using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Layers;
using SkiaSharp;
using System;
using System.Linq;

namespace KyoshinEewViewer.Series.ShakeDetectionVerifier;

/// <summary>
/// 揺れ検知検証用のレイヤー
/// KyoshinMonitorLayerを参考に、観測点とイベントを表示する
/// </summary>
public class ShakeDetectionVerifierLayer(KyoshinEewViewerConfiguration config) : MapLayer
{
	private RealtimeObservationPoint[]? _observationPoints;
	public RealtimeObservationPoint[]? ObservationPoints
	{
		get => _observationPoints;
		set
		{
			_observationPoints = value;
			RefreshRequest();
		}
	}

	private KyoshinEvent[]? _kyoshinEvents;
	public KyoshinEvent[]? KyoshinEvents
	{
		get => _kyoshinEvents;
		set
		{
			_kyoshinEvents = value;
			RefreshRequest();
		}
	}

	/// <summary>
	/// 現在時刻（イベント終了までの時間計算用）
	/// </summary>
	public DateTime CurrentTime { get; set; }

	private static readonly SKPaint PointPaint = new()
	{
		Style = SKPaintStyle.Fill,
		IsAntialias = true,
		StrokeWidth = 2,
	};

	private static readonly SKPaint InvalidatePaint = new()
	{
		Style = SKPaintStyle.Stroke,
		IsAntialias = true,
		Color = SKColors.Gray,
		StrokeWidth = 1,
	};

	private static readonly SKPaint EventPaint = new()
	{
		Style = SKPaintStyle.Stroke,
		IsAntialias = true,
		StrokeWidth = 2,
	};

	private static readonly SKPaint EventCenterPaint = new()
	{
		Style = SKPaintStyle.Stroke,
		IsAntialias = true,
		StrokeWidth = 3,
	};

	private static readonly SKPaint ShadowPaint = new()
	{
		Style = SKPaintStyle.Fill,
		IsAntialias = true,
		Color = new SKColor(0, 0, 0, 80),
	};

#if DEBUG
	private static readonly SKPaint TextPaint = new()
	{
		Typeface = KyoshinEewViewerFonts.MainRegular,
		TextSize = 12,
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
#endif

	private KyoshinEewViewerConfiguration Config { get; } = config;
	private bool IsDarkTheme { get; set; }

	public override bool NeedPersistentUpdate => false;

	public override void RefreshResourceCache(WindowTheme windowTheme)
	{
		IsDarkTheme = windowTheme.IsDark;
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

				// 描画対象の観測点のリストアップ
				var ordersRenderedPoints = ObservationPoints
					.Where(point =>
					{
						// 設定以下の震度であれば描画しない
						if (point.LatestIntensity != null && point.LatestIntensity < Config.RawIntensityObject.MinShownIntensity)
							return false;

						var circleSize = (float)(Math.Max(1, zoom - 4) * 1.75);
						var circleVector = new PointD(circleSize, circleSize);
						var pointCenter = point.Location.ToPixel(zoom);
						var bound = new RectD(pointCenter - circleVector, pointCenter + circleVector);
						if (!pixelBound.IntersectsWith(bound))
							return false;

						// 観測震度が取得できず、過去に観測履歴が存在し、設定で観測できない地点の描画設定が有効であれば描画対象として登録する
						if (point.LatestIntensity == null && (
							!Config.RawIntensityObject.ShowInvalidateIcon ||
							!point.HasValidHistory))
							return false;

						// 異常値除外
						if (!Config.RawIntensityObject.ShowInvalidateIcon && point.IsTmpDisabled)
							return false;

						return true;
					})
					.OrderByDescending(p => p.LatestIntensity ?? -1000);

				var renderedPoints = ordersRenderedPoints.ToArray();

#if DEBUG
				// ズーム10以上で揺れ検知パラメータを表示
				if (zoom >= 10)
				{
					foreach (var point in renderedPoints)
					{
						if (point.LatestIntensity == null || point.IsTmpDisabled)
							continue;

						var circleSize = (float)(Math.Max(1, zoom - 4) * 1.75);
						var pointCenter = point.Location.ToPixel(zoom);

						// 1行目: 計測震度とイベント終了までの時間
						string remainingText;
						if (point.Event != null && point.EventedExpireAt > CurrentTime)
						{
							var remaining = point.EventedExpireAt - CurrentTime;
							remainingText = $"{remaining.TotalSeconds:F1}";
						}
						else if (point.Event != null)
						{
							remainingText = "切";
						}
						else
						{
							remainingText = "--";
						}
						var line1 = $"I:{point.LatestIntensity:F1} R:{remainingText}";

						// 2行目: 既存のデバッグ表示
						// Diff: 震度上昇値, Scr: スコア, Thr: 閾値, Wgt: 近傍重み
						var line2 = $"D:{point.IntensityDiff:+0.00;-0.00} S:{point.DebugDetectionScore:F2}/{point.DebugDetectionThreshold:F2} W:{point.DebugAvailableTotalWeight:F1}";
						if (point.DebugIsIsolated)
							line2 = $"D:{point.IntensityDiff:+0.00;-0.00} [離島]";

						var line1Width = TextPaint.MeasureText(line1);
						var line2Width = TextPaint.MeasureText(line2);
						var maxWidth = Math.Max(line1Width, line2Width);
						var textX = (float)(pointCenter.X + circleSize + 4);
						var line1Y = (float)(pointCenter.Y - TextPaint.TextSize * 0.3);
						var line2Y = (float)(pointCenter.Y + TextPaint.TextSize * 0.9);
						var lineHeight = TextPaint.TextSize + 2;

						// 背景の描画（2行分）
						TextBackgroundPaint.Color = point.LatestColor ?? SKColors.Gray;
						canvas.DrawRect(
							textX - 2,
							line1Y - TextPaint.TextSize,
							maxWidth + 4,
							lineHeight * 2 + 4,
							TextBackgroundPaint);

						// 1行目の描画（アウトライン）
						TextPaint.Style = SKPaintStyle.Stroke;
						TextPaint.Color = IsDarkTheme ? SKColors.Black : SKColors.White;
						canvas.DrawText(line1, textX, line1Y, TextPaint);

						// 1行目の描画（本体）
						TextPaint.Style = SKPaintStyle.Fill;
						TextPaint.Color = IsDarkTheme ? SKColors.White : SKColors.Black;
						canvas.DrawText(line1, textX, line1Y, TextPaint);

						// 2行目の描画（アウトライン）
						TextPaint.Style = SKPaintStyle.Stroke;
						TextPaint.Color = IsDarkTheme ? SKColors.Black : SKColors.White;
						canvas.DrawText(line2, textX, line2Y, TextPaint);

						// 2行目の描画（本体）
						TextPaint.Style = SKPaintStyle.Fill;
						TextPaint.Color = IsDarkTheme ? SKColors.White : SKColors.Black;
						canvas.DrawText(line2, textX, line2Y, TextPaint);
					}
				}
#endif

				// 観測点本体の描画
				foreach (var point in renderedPoints.Reverse())
				{
					// 描画しない
					if (point.LatestIntensity != null && point.LatestIntensity < Config.RawIntensityObject.MinShownIntensity)
						continue;

					var circleSize = (float)(Math.Max(1, zoom - 4) * 1.75);
					var pointCenter = point.Location.ToPixel(zoom);

					var color = point.LatestColor;

					// 無効な観測点
					if (point.LatestIntensity == null || point.IsTmpDisabled)
					{
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
						// ズーム6以上で影を描画
						if (zoom >= 6)
						{
							var shadowOffset = circleSize * 0.15f;
							var shadowLayers = (int)Math.Floor(zoom / 4);

							for (var i = shadowLayers; i > 0; i--)
							{
								var layerAlpha = (byte)(80 / shadowLayers);
								var layerSize = circleSize + (i * shadowOffset * 0.5f);
								ShadowPaint.Color = new SKColor(0, 0, 0, layerAlpha);

								canvas.DrawCircle(
									pointCenter.AsSkPoint(),
									layerSize,
									ShadowPaint);
							}
						}

						PointPaint.Color = color.Value;
						canvas.DrawCircle(
							pointCenter.AsSkPoint(),
							circleSize,
							PointPaint);
					}

					// イベントに属している場合はマーカーを描画
					if (point.Event != null)
					{
						EventPaint.Color = point.Event.DebugColor;
						canvas.DrawCircle(
							pointCenter.AsSkPoint(),
							circleSize / 2,
							EventPaint);
					}
				}
			}

			// イベントの範囲を描画
			if (KyoshinEvents != null)
			{
				foreach (var evt in KyoshinEvents)
				{
					EventPaint.Color = evt.DebugColor;
					var tl = evt.TopLeft.ToPixel(zoom).AsSkPoint();
					var br = evt.BottomRight.ToPixel(zoom).AsSkPoint() - tl;
					canvas.DrawRect(tl.X, tl.Y, br.X, br.Y, EventPaint);

					// イベント中心に固定サイズの円を描画（1件目の観測点座標を使用）
					if (evt.Points.Count > 0)
					{
						var centerLocation = evt.Points[0].Location;
						var centerPixel = centerLocation.ToPixel(zoom).AsSkPoint();
						EventCenterPaint.Color = evt.DebugColor;
						canvas.DrawCircle(centerPixel, 20, EventCenterPaint);
					}
				}
			}
		}
		finally
		{
			canvas.Restore();
		}
	}
}
