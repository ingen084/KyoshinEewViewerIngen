using Avalonia.Input;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Layers;
using KyoshinMonitorLib;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Series.ShakeDetectionVerifier;

/// <summary>
/// 揺れ検知検証用のレイヤー
/// KyoshinMonitorLayerを参考に、観測点とイベントを表示する
/// </summary>
public class ShakeDetectionVerifierLayer(KyoshinEewViewerConfiguration config) : MapLayer
{
	/// <summary>
	/// 観測点がクリックされた時のイベント
	/// </summary>
	public event Action<RealtimeObservationPoint>? ObservationPointClicked;

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
		IsAntialias = true,
		SubpixelText = true,
		LcdRenderText = true,
		Style = SKPaintStyle.Fill,
	};

	private static readonly SKPaint TextBackgroundPaint = new()
	{
		IsAntialias = true,
		Color = new SKColor(80, 80, 80),
		Style = SKPaintStyle.Fill,
	};

	private static readonly SKPaint TextBorderPaint = new()
	{
		IsAntialias = true,
		Color = SKColors.White,
		Style = SKPaintStyle.Stroke,
		StrokeWidth = 2,
	};
#endif

	private KyoshinEewViewerConfiguration Config { get; } = config;

	public override bool NeedPersistentUpdate => false;

	public override void RefreshResourceCache(WindowTheme _) { }

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

						// 2行目: スコアと閾値
						// 3行目: ペナルティと近傍重み
						// 4行目: 検知時刻
						string line2, line3, line4, line5;
						if (point.DebugIsIsolated)
						{
							line2 = $"D:{point.IntensityDiff:+0.00;-0.00} [離島]";
							line3 = "";
							line4 = "";
							line5 = "";
						}
						else
						{
							line2 = $"D:{point.IntensityDiff:+0.00;-0.00} S:{point.DebugDetectionScore:F2}/{point.DebugDetectionThreshold:F2}";
							line3 = $"P:{point.DebugNoChangePenalty:F2} W:{point.DebugAvailableTotalWeight:F1}";
							line4 = point.Event != null ? $"検知: {point.InitialEventedAt:HH:mm:ss}" : "";
							line5 = "";
						}

						var line1Width = TextPaint.MeasureText(line1);
						var line2Width = TextPaint.MeasureText(line2);
						var line3Width = TextPaint.MeasureText(line3);
						var line4Width = TextPaint.MeasureText(line4);
						var line5Width = TextPaint.MeasureText(line5);
						var maxWidth = Math.Max(Math.Max(Math.Max(Math.Max(line1Width, line2Width), line3Width), line4Width), line5Width);
						var textX = (float)(pointCenter.X + circleSize + 4);
						var line1Y = (float)(pointCenter.Y - TextPaint.TextSize * 0.5);
						var line2Y = (float)(pointCenter.Y + TextPaint.TextSize * 0.7);
						var line3Y = (float)(pointCenter.Y + TextPaint.TextSize * 1.9);
						var line4Y = (float)(pointCenter.Y + TextPaint.TextSize * 3.1);
						var line5Y = (float)(pointCenter.Y + TextPaint.TextSize * 4.3);
						var lineHeight = TextPaint.TextSize + 2;
						var lineCount = 1;
						if (!string.IsNullOrEmpty(line2)) lineCount++;
						if (!string.IsNullOrEmpty(line3)) lineCount++;
						if (!string.IsNullOrEmpty(line4)) lineCount++;
						if (!string.IsNullOrEmpty(line5)) lineCount++;

						// 背景の描画（グレー統一 + 枠：検知中は赤、それ以外は白）
						var bgRect = new SKRect(
							textX - 2,
							line1Y - TextPaint.TextSize,
							textX + maxWidth + 2,
							line1Y - TextPaint.TextSize + lineHeight * lineCount + 4);
						canvas.DrawRect(bgRect, TextBackgroundPaint);
						TextBorderPaint.Color = point.Event != null ? SKColors.Red : SKColors.White;
						canvas.DrawRect(bgRect, TextBorderPaint);

						// 1行目の描画
						TextPaint.Color = SKColors.White;
						canvas.DrawText(line1, textX, line1Y, TextPaint);

						// 2行目の描画
						if (!string.IsNullOrEmpty(line2))
							canvas.DrawText(line2, textX, line2Y, TextPaint);

						// 3行目の描画（離島でない場合のみ）
						if (!string.IsNullOrEmpty(line3))
							canvas.DrawText(line3, textX, line3Y, TextPaint);

						// 4行目の描画（検知時刻）
						if (!string.IsNullOrEmpty(line4))
							canvas.DrawText(line4, textX, line4Y, TextPaint);

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

	public override bool OnMouseClick(Location location, PointD screenPosition, MouseButton button, LayerRenderParameter param)
	{
		if (ObservationPoints == null || button != MouseButton.Left)
			return false;

		// クリックした位置の近くにある観測点を探す
		var candidates = new List<(RealtimeObservationPoint Point, double Distance)>();

		foreach (var point in ObservationPoints)
		{
			// 画面座標での距離を計算
			var pixelPoint = point.Location.ToPixel(param.Zoom) - param.LeftTopPixel;
			var distance = Math.Sqrt(Math.Pow(pixelPoint.X - screenPosition.X, 2) + Math.Pow(pixelPoint.Y - screenPosition.Y, 2));

			// クリック判定（10ピクセル以内）
			if (distance < 10)
				candidates.Add((point, distance));
		}

		if (candidates.Count == 0)
			return false;

		// 最も近い観測点を選択
		var nearest = candidates.OrderBy(c => c.Distance).First().Point;
		ObservationPointClicked?.Invoke(nearest);
		return true;
	}
}
