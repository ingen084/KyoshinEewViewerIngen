using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Series.ShakeDetectionVerifier.Controls;

/// <summary>
/// 残差グラフ描画コントロール
/// X軸: 震央からの距離 (km)
/// Y軸: 時刻（検知時刻と理想検知時刻の差分を表示）
/// </summary>
public class ResidualChartControl : Control
{
	#region Data
	public static readonly StyledProperty<IReadOnlyList<StationResidualData>?> DataProperty =
		AvaloniaProperty.Register<ResidualChartControl, IReadOnlyList<StationResidualData>?>(nameof(Data));

	public IReadOnlyList<StationResidualData>? Data
	{
		get => GetValue(DataProperty);
		set => SetValue(DataProperty, value);
	}
	#endregion

	#region OriginTime
	public static readonly StyledProperty<DateTime> OriginTimeProperty =
		AvaloniaProperty.Register<ResidualChartControl, DateTime>(nameof(OriginTime));

	public DateTime OriginTime
	{
		get => GetValue(OriginTimeProperty);
		set => SetValue(OriginTimeProperty, value);
	}
	#endregion

	#region Title
	public static readonly StyledProperty<string> TitleProperty =
		AvaloniaProperty.Register<ResidualChartControl, string>(nameof(Title), "Travel Time Curve");

	public string Title
	{
		get => GetValue(TitleProperty);
		set => SetValue(TitleProperty, value);
	}
	#endregion

	static ResidualChartControl()
	{
		AffectsRender<ResidualChartControl>(DataProperty, OriginTimeProperty, TitleProperty);
	}

	public override void Render(DrawingContext context)
	{
		base.Render(context);

		var bounds = new Rect(0, 0, Bounds.Width, Bounds.Height);
		if (bounds.Width <= 0 || bounds.Height <= 0)
			return;

		context.Custom(new ResidualChartRenderOperation(bounds, Data, Title));
	}

	private class ResidualChartRenderOperation : ICustomDrawOperation
	{
		private static readonly SKColor BackgroundColor = new(30, 30, 30);
		private static readonly SKColor GridColor = new(60, 60, 60);
		private static readonly SKColor AxisColor = new(200, 200, 200);
		private static readonly SKColor TheoreticalPColor = new(100, 150, 255);
		private static readonly SKColor TheoreticalSColor = new(255, 150, 100);
		private static readonly SKColor DetectedColor = new(100, 255, 100);
		private static readonly SKColor ResidualLineColor = new(255, 255, 255, 100);

		// Mono Font (Consolas, Menlo, Monaco, monospace)
		private static readonly SKTypeface MonoTypeface = SKTypeface.FromFamilyName(
			"Consolas",
			SKFontStyleWeight.Normal,
			SKFontStyleWidth.Normal,
			SKFontStyleSlant.Upright) ?? SKTypeface.Default;

		public Rect Bounds { get; }
		private IReadOnlyList<StationResidualData>? Data { get; }
		private string Title { get; }

		public ResidualChartRenderOperation(Rect bounds, IReadOnlyList<StationResidualData>? data, string title)
		{
			Bounds = bounds;
			Data = data;
			Title = title;
		}

		public void Dispose() => GC.SuppressFinalize(this);
		public bool Equals(ICustomDrawOperation? other) => false;
		public bool HitTest(Point p) => false;

		public void Render(ImmediateDrawingContext context)
		{
			var leaseFeature = context.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature)) as ISkiaSharpApiLeaseFeature;
			if (leaseFeature == null)
				return;
			using var lease = leaseFeature.Lease();
			var canvas = lease.SkCanvas;

			RenderChart(canvas, (float)Bounds.Width, (float)Bounds.Height);
		}

		private void RenderChart(SKCanvas canvas, float width, float height)
		{
			const float marginLeft = 60;
			const float marginRight = 20;
			const float marginTop = 30;
			const float marginBottom = 40;

			var chartWidth = width - marginLeft - marginRight;
			var chartHeight = height - marginTop - marginBottom;

			if (chartWidth <= 0 || chartHeight <= 0)
				return;

			// 背景
			using var backgroundPaint = new SKPaint { Color = BackgroundColor };
			canvas.DrawRect(0, 0, width, height, backgroundPaint);

			var data = Data;
			if (data == null || data.Count == 0)
			{
				DrawNoDataMessage(canvas, width, height);
				return;
			}

			// データ範囲を計算
			var maxDistance = data.Max(d => d.DistanceKm);
			var maxTime = data.Max(d => Math.Max(
				d.DetectedOffsetSeconds,
				Math.Max(d.TheoreticalPOffsetSeconds ?? 0, d.TheoreticalSOffsetSeconds ?? 0)));
			var minTime = data.Min(d => Math.Min(
				d.DetectedOffsetSeconds,
				Math.Min(d.TheoreticalPOffsetSeconds ?? double.MaxValue, d.TheoreticalSOffsetSeconds ?? double.MaxValue)));

			// マージンを追加
			maxDistance = Math.Max(maxDistance * 1.1, 10);
			maxTime = Math.Max(maxTime * 1.1, 10);
			minTime = Math.Min(minTime - 1, 0);

			// 軸を描画
			DrawAxes(canvas, marginLeft, marginTop, chartWidth, chartHeight, maxDistance, minTime, maxTime);

			// タイトルを描画
			DrawTitle(canvas, width, marginTop);

			// データをプロット
			PlotData(canvas, data, marginLeft, marginTop, chartWidth, chartHeight, maxDistance, minTime, maxTime);

			// 凡例を描画
			DrawLegend(canvas, width - marginRight - 120, marginTop + 10);
		}

		private static void DrawNoDataMessage(SKCanvas canvas, float width, float height)
		{
			using var paint = new SKPaint
			{
				Color = AxisColor,
				TextSize = 14,
				IsAntialias = true,
				TextAlign = SKTextAlign.Center,
				Typeface = MonoTypeface
			};
			canvas.DrawText("No Data", width / 2, height / 2, paint);
		}

		private static void DrawAxes(SKCanvas canvas, float left, float top, float chartWidth, float chartHeight,
			double maxDistance, double minTime, double maxTime)
		{
			using var axisPaint = new SKPaint { Color = AxisColor, StrokeWidth = 1, IsAntialias = true };
			using var gridPaint = new SKPaint { Color = GridColor, StrokeWidth = 0.5f, IsAntialias = true };
			using var textPaint = new SKPaint { Color = AxisColor, TextSize = 10, IsAntialias = true, Typeface = MonoTypeface };

			// X軸
			canvas.DrawLine(left, top + chartHeight, left + chartWidth, top + chartHeight, axisPaint);
			// Y軸
			canvas.DrawLine(left, top, left, top + chartHeight, axisPaint);

			// X軸グリッドとラベル（距離）
			var distanceStep = CalculateAxisStep(maxDistance, 5);
			for (var d = 0.0; d <= maxDistance; d += distanceStep)
			{
				var x = left + (float)(d / maxDistance * chartWidth);
				canvas.DrawLine(x, top, x, top + chartHeight, gridPaint);

				textPaint.TextAlign = SKTextAlign.Center;
				canvas.DrawText($"{d:F0}", x, top + chartHeight + 15, textPaint);
			}

			// X軸ラベル
			textPaint.TextAlign = SKTextAlign.Center;
			canvas.DrawText("Epicentral Distance (km)", left + chartWidth / 2, top + chartHeight + 32, textPaint);

			// Y軸グリッドとラベル（時刻）
			var timeRange = maxTime - minTime;
			var timeStep = CalculateAxisStep(timeRange, 5);
			for (var t = Math.Ceiling(minTime / timeStep) * timeStep; t <= maxTime; t += timeStep)
			{
				var y = top + chartHeight - (float)((t - minTime) / timeRange * chartHeight);
				canvas.DrawLine(left, y, left + chartWidth, y, gridPaint);

				textPaint.TextAlign = SKTextAlign.Right;
				canvas.DrawText($"{t:F0}s", left - 5, y + 4, textPaint);
			}

			// Y軸ラベル
			canvas.Save();
			canvas.RotateDegrees(-90, left - 45, top + chartHeight / 2);
			textPaint.TextAlign = SKTextAlign.Center;
			canvas.DrawText("Time from Origin (s)", left - 45, top + chartHeight / 2, textPaint);
			canvas.Restore();
		}

		private void DrawTitle(SKCanvas canvas, float width, float marginTop)
		{
			using var paint = new SKPaint
			{
				Color = AxisColor,
				TextSize = 14,
				IsAntialias = true,
				TextAlign = SKTextAlign.Center,
				FakeBoldText = true,
				Typeface = MonoTypeface
			};
			canvas.DrawText(Title, width / 2, marginTop - 10, paint);
		}

		private static void DrawLegend(SKCanvas canvas, float x, float y)
		{
			using var textPaint = new SKPaint { Color = AxisColor, TextSize = 10, IsAntialias = true, Typeface = MonoTypeface };
			using var pPaint = new SKPaint { Color = TheoreticalPColor, StrokeWidth = 2, IsAntialias = true };
			using var sPaint = new SKPaint { Color = TheoreticalSColor, StrokeWidth = 2, IsAntialias = true };
			using var detectedPaint = new SKPaint { Color = DetectedColor, IsAntialias = true };

			var lineHeight = 16f;

			// P-wave
			canvas.DrawLine(x, y + 5, x + 20, y + 5, pPaint);
			canvas.DrawText("P-wave", x + 25, y + 9, textPaint);
			y += lineHeight;

			// S-wave
			canvas.DrawLine(x, y + 5, x + 20, y + 5, sPaint);
			canvas.DrawText("S-wave", x + 25, y + 9, textPaint);
			y += lineHeight;

			// Detected
			canvas.DrawCircle(x + 10, y + 5, 4, detectedPaint);
			canvas.DrawText("Detected", x + 25, y + 9, textPaint);
		}

		private static void PlotData(SKCanvas canvas, IReadOnlyList<StationResidualData> data,
			float left, float top, float chartWidth, float chartHeight,
			double maxDistance, double minTime, double maxTime)
		{
			var timeRange = maxTime - minTime;

			using var pPaint = new SKPaint { Color = TheoreticalPColor, StrokeWidth = 2, IsAntialias = true, Style = SKPaintStyle.Stroke };
			using var sPaint = new SKPaint { Color = TheoreticalSColor, StrokeWidth = 2, IsAntialias = true, Style = SKPaintStyle.Stroke };
			using var detectedPaint = new SKPaint { Color = DetectedColor, IsAntialias = true };
			using var residualPaint = new SKPaint { Color = ResidualLineColor, StrokeWidth = 1, IsAntialias = true };

			// P波・S波の走時曲線を描画
			var sortedData = data.OrderBy(d => d.DistanceKm).ToList();

			// P波曲線
			var pPath = new SKPath();
			var pStarted = false;
			foreach (var point in sortedData.Where(d => d.TheoreticalPOffsetSeconds.HasValue))
			{
				var x = left + (float)(point.DistanceKm / maxDistance * chartWidth);
				var y = top + chartHeight - (float)((point.TheoreticalPOffsetSeconds!.Value - minTime) / timeRange * chartHeight);

				if (!pStarted)
				{
					pPath.MoveTo(x, y);
					pStarted = true;
				}
				else
				{
					pPath.LineTo(x, y);
				}
			}
			if (pStarted)
				canvas.DrawPath(pPath, pPaint);

			// S波曲線
			var sPath = new SKPath();
			var sStarted = false;
			foreach (var point in sortedData.Where(d => d.TheoreticalSOffsetSeconds.HasValue))
			{
				var x = left + (float)(point.DistanceKm / maxDistance * chartWidth);
				var y = top + chartHeight - (float)((point.TheoreticalSOffsetSeconds!.Value - minTime) / timeRange * chartHeight);

				if (!sStarted)
				{
					sPath.MoveTo(x, y);
					sStarted = true;
				}
				else
				{
					sPath.LineTo(x, y);
				}
			}
			if (sStarted)
				canvas.DrawPath(sPath, sPaint);

			// 各観測点のデータをプロット
			foreach (var point in data)
			{
				var x = left + (float)(point.DistanceKm / maxDistance * chartWidth);
				var detectedY = top + chartHeight - (float)((point.DetectedOffsetSeconds - minTime) / timeRange * chartHeight);

				// 理論到達時刻との差分を線で結ぶ
				// S波との差分を優先（より小さい方と結ぶ）
				double? theoreticalOffset = null;
				if (point.TheoreticalSOffsetSeconds.HasValue && point.TheoreticalPOffsetSeconds.HasValue)
				{
					var sResidual = Math.Abs(point.DetectedOffsetSeconds - point.TheoreticalSOffsetSeconds.Value);
					var pResidual = Math.Abs(point.DetectedOffsetSeconds - point.TheoreticalPOffsetSeconds.Value);
					theoreticalOffset = sResidual < pResidual
						? point.TheoreticalSOffsetSeconds
						: point.TheoreticalPOffsetSeconds;
				}
				else
				{
					theoreticalOffset = point.TheoreticalSOffsetSeconds ?? point.TheoreticalPOffsetSeconds;
				}

				if (theoreticalOffset.HasValue)
				{
					var theoreticalY = top + chartHeight - (float)((theoreticalOffset.Value - minTime) / timeRange * chartHeight);
					canvas.DrawLine(x, detectedY, x, theoreticalY, residualPaint);
				}

				// 検知時刻の点を描画
				canvas.DrawCircle(x, detectedY, 4, detectedPaint);
			}
		}

		private static double CalculateAxisStep(double range, int targetSteps)
		{
			if (range <= 0) return 1;

			var roughStep = range / targetSteps;
			var magnitude = Math.Pow(10, Math.Floor(Math.Log10(roughStep)));
			var normalizedStep = roughStep / magnitude;

			double niceStep;
			if (normalizedStep < 1.5)
				niceStep = 1;
			else if (normalizedStep < 3)
				niceStep = 2;
			else if (normalizedStep < 7)
				niceStep = 5;
			else
				niceStep = 10;

			return niceStep * magnitude;
		}
	}
}

/// <summary>
/// 観測点ごとの残差データ
/// </summary>
public record class StationResidualData
{
	/// <summary>
	/// 観測点コード
	/// </summary>
	public required string StationCode { get; init; }

	/// <summary>
	/// 観測点名
	/// </summary>
	public required string StationName { get; init; }

	/// <summary>
	/// 震央からの距離 (km)
	/// </summary>
	public required double DistanceKm { get; init; }

	/// <summary>
	/// 検知時刻（発震時刻からのオフセット秒）
	/// </summary>
	public required double DetectedOffsetSeconds { get; init; }

	/// <summary>
	/// P波理論到達時刻（発震時刻からのオフセット秒）
	/// </summary>
	public double? TheoreticalPOffsetSeconds { get; init; }

	/// <summary>
	/// S波理論到達時刻（発震時刻からのオフセット秒）
	/// </summary>
	public double? TheoreticalSOffsetSeconds { get; init; }

	/// <summary>
	/// 残差（検知時刻 - 最も近い理論到達時刻）
	/// </summary>
	public double? ResidualSeconds { get; init; }
}
