using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Layers;
using KyoshinEewViewer.Series.KyoshinMonitor.Services;
using KyoshinEewViewer.Series.Ml.Services;
using KyoshinMonitorLib;
using SkiaSharp;
using System;

namespace KyoshinEewViewer.Series.Ml.Layers;

/// <summary>
/// データセット作成用レイヤー
/// 強震モニタの観測点と地震情報（震度DB）を表示する
/// </summary>
public class MlDatasetLayer : MapLayer
{
	/// <summary>
	/// 観測点データ
	/// </summary>
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

	/// <summary>
	/// 現在表示中の地震情報
	/// </summary>
	private ShindoDbEarthquake[]? _activeEarthquakes;
	public ShindoDbEarthquake[]? ActiveEarthquakes
	{
		get => _activeEarthquakes;
		set
		{
			_activeEarthquakes = value;
			RefreshRequest();
		}
	}

	/// <summary>
	/// 現在の表示時刻
	/// </summary>
	public DateTime CurrentTime { get; set; }

	public override bool NeedPersistentUpdate => false;

	// 観測点描画用ペイント
	private static readonly SKPaint PointPaint = new()
	{
		Style = SKPaintStyle.Fill,
		IsAntialias = true,
	};

	private static readonly SKPaint InvalidatePaint = new()
	{
		Style = SKPaintStyle.Stroke,
		IsAntialias = true,
		Color = SKColors.Gray,
		StrokeWidth = 1,
	};

	// 震央描画用ペイント
	private SKPaint HypocenterBorderPaint { get; } = new()
	{
		Style = SKPaintStyle.Stroke,
		StrokeWidth = 6,
		IsAntialias = true,
	};

	private SKPaint HypocenterPaint { get; } = new()
	{
		Style = SKPaintStyle.Stroke,
		StrokeWidth = 4,
		IsAntialias = true,
	};

	// P/S波描画用ペイント
	private SKPaint PWavePaint { get; } = new()
	{
		IsAntialias = true,
		Style = SKPaintStyle.Stroke,
		StrokeWidth = 1,
	};

	private SKPaint SWavePaint { get; } = new()
	{
		IsAntialias = true,
		Style = SKPaintStyle.Stroke,
		StrokeWidth = 1,
	};

	// 情報ラベル用
	private static readonly SKPaint LabelBackgroundPaint = new()
	{
		Style = SKPaintStyle.Fill,
		Color = new SKColor(0, 0, 0, 180),
		IsAntialias = true,
	};

	private static readonly SKPaint LabelTextPaint = new()
	{
		Typeface = KyoshinEewViewerFonts.MainRegular,
		TextSize = 12,
		Color = SKColors.White,
		IsAntialias = true,
	};

	// テーマから取得した色
	private SKColor HypocenterBorderColor { get; set; }
	private SKColor HypocenterColor { get; set; }
	private SKColor PWaveColor { get; set; }
	private SKColor SWaveColor { get; set; }
	private bool IsSWaveGradient { get; set; }

	public override void RefreshResourceCache(WindowTheme windowTheme)
	{
		// テーマから予報用の色を取得（震度DBの地震は予報扱い）
		HypocenterBorderColor = SKColor.Parse(windowTheme.EewForecastHypocenterBorderColor);
		HypocenterColor = SKColor.Parse(windowTheme.EewForecastHypocenterColor);
		PWaveColor = SKColor.Parse(windowTheme.EewForecastPWaveColor);
		SWaveColor = SKColor.Parse(windowTheme.EewForecastSWaveColor);
		IsSWaveGradient = windowTheme.IsEewForecastSWaveGradient;

		// ペイントに色を適用
		HypocenterBorderPaint.Color = HypocenterBorderColor;
		HypocenterPaint.Color = HypocenterColor;
		PWavePaint.Color = PWaveColor;
		SWavePaint.Color = SWaveColor;
	}

	public override void Render(SKCanvas canvas, LayerRenderParameter param, bool isAnimating)
	{
		canvas.Save();
		try
		{
			var zoom = param.Zoom;
			canvas.Translate((float)-param.LeftTopPixel.X, (float)-param.LeftTopPixel.Y);

			var pixelBound = param.PixelBound;

			// 観測点の描画
			RenderObservationPoints(canvas, zoom, pixelBound);

			// 地震情報の描画（緊急地震速報風）
			RenderEarthquakes(canvas, zoom);
		}
		finally
		{
			canvas.Restore();
		}
	}

	/// <summary>
	/// 観測点を描画する
	/// </summary>
	private void RenderObservationPoints(SKCanvas canvas, double zoom, RectD pixelBound)
	{
		if (ObservationPoints == null)
			return;

		foreach (var point in ObservationPoints)
		{
			var circleSize = (float)(Math.Max(1, zoom - 4) * 1.75);
			var circleVector = new PointD(circleSize, circleSize);
			var pointCenter = point.Location.ToPixel(zoom);
			var bound = new RectD(pointCenter - circleVector, pointCenter + circleVector);

			if (!pixelBound.IntersectsWith(bound))
				continue;

			// 観測震度がない場合
			if (point.LatestIntensity == null)
			{
				canvas.DrawCircle(
					pointCenter.AsSkPoint(),
					circleSize,
					InvalidatePaint);
				continue;
			}

			// 色がある場合
			if (point.LatestColor is { } color)
			{
				PointPaint.Color = color;
				canvas.DrawCircle(
					pointCenter.AsSkPoint(),
					circleSize,
					PointPaint);
			}
		}
	}

	/// <summary>
	/// 地震情報を緊急地震速報風に描画する
	/// </summary>
	private void RenderEarthquakes(SKCanvas canvas, double zoom)
	{
		if (ActiveEarthquakes == null || ActiveEarthquakes.Length == 0)
			return;

		foreach (var eq in ActiveEarthquakes)
		{
			if (eq.Location == null || eq.OccurrenceTime == null)
				continue;

			var basePoint = eq.Location.ToPixel(zoom);

			// 震央マーク（×）の描画
			var minSize = 8 + (zoom - 5) * 1.25;
			var maxSize = minSize + 1;

			canvas.DrawLine(
				(basePoint - new PointD(maxSize, maxSize)).AsSkPoint(),
				(basePoint + new PointD(maxSize, maxSize)).AsSkPoint(),
				HypocenterBorderPaint);
			canvas.DrawLine(
				(basePoint - new PointD(-maxSize, maxSize)).AsSkPoint(),
				(basePoint + new PointD(-maxSize, maxSize)).AsSkPoint(),
				HypocenterBorderPaint);
			canvas.DrawLine(
				(basePoint - new PointD(minSize, minSize)).AsSkPoint(),
				(basePoint + new PointD(minSize, minSize)).AsSkPoint(),
				HypocenterPaint);
			canvas.DrawLine(
				(basePoint - new PointD(-minSize, minSize)).AsSkPoint(),
				(basePoint + new PointD(-minSize, minSize)).AsSkPoint(),
				HypocenterPaint);

			// P/S波の描画（深さを10km単位に四捨五入）
			var rawDepth = eq.Depth ?? 10;
			var depth = (int)Math.Round(rawDepth / 10.0) * 10;
			if (depth < 10)
				depth = 10;

			(var p, var s) = TravelTimeTableService.CalcDistance(
				eq.OccurrenceTime.Value,
				CurrentTime,
				depth);

			if (p is { } pDistance && pDistance > 0)
			{
				using var circle = PathGenerator.MakeCirclePath(eq.Location, pDistance * 1000, zoom);
				canvas.DrawPath(circle, PWavePaint);
			}

			if (s is { } sDistance && sDistance > 0)
			{
				using var circle = PathGenerator.MakeCirclePath(eq.Location, sDistance * 1000, zoom);

				// グラデーション塗りつぶし（テーマ設定に従う）
				if (IsSWaveGradient)
				{
					using var gradPaint = new SKPaint
					{
						IsAntialias = true,
						Style = SKPaintStyle.Fill,
						Shader = SKShader.CreateRadialGradient(
							basePoint.AsSkPoint(),
							circle.Bounds.Height / 2,
							[SWaveColor.WithAlpha(15), SWaveColor.WithAlpha(80)],
							[.6f, 1f],
							SKShaderTileMode.Clamp
						)
					};
					canvas.DrawPath(circle, gradPaint);
				}
				canvas.DrawPath(circle, SWavePaint);
			}

			// 地震情報のラベルを描画（震央の右下に）
			var labelText = $"{eq.Name} M{eq.Mag} {eq.Dep} {eq.MaxI}";
			var labelWidth = LabelTextPaint.MeasureText(labelText);
			var labelX = (float)(basePoint.X + maxSize + 5);
			var labelY = (float)(basePoint.Y + maxSize + 5);
			var labelRect = new SKRect(labelX - 3, labelY - 12, labelX + labelWidth + 3, labelY + 3);

			canvas.DrawRoundRect(labelRect, 3, 3, LabelBackgroundPaint);
			canvas.DrawText(labelText, labelX, labelY, LabelTextPaint);
		}
	}
}
