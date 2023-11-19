using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.CustomControl;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PiDASPlusGraph;
public class GraphControl : Avalonia.Controls.Control, ICustomDrawOperation
{
	private float _minValue = -1;
	public static readonly DirectProperty<GraphControl, float> MinValueProperty =
		AvaloniaProperty.RegisterDirect<GraphControl, float>(
			nameof(MinValue),
			o => o.MinValue,
			(o, v) => o.MinValue = v
		);
	public float MinValue
	{
		get => _minValue;
		set {
			if (_minValue == value)
				return;
			_minValue = value;
			Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
		}
	}

	private float _maxValue = 1;
	public static readonly DirectProperty<GraphControl, float> MaxValueProperty =
		AvaloniaProperty.RegisterDirect<GraphControl, float>(
			nameof(MaxValue),
			o => o.MaxValue,
			(o, v) => o.MaxValue = v
		);
	public float MaxValue
	{
		get => _maxValue;
		set {
			if (_maxValue == value)
				return;
			_maxValue = value;
			Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
		}
	}

	private bool _isAutoRange = false;
	public static readonly DirectProperty<GraphControl, bool> IsAutoRangeProperty =
		AvaloniaProperty.RegisterDirect<GraphControl, bool>(
			nameof(IsAutoRange),
			o => o.IsAutoRange,
			(o, v) => o.IsAutoRange = v
		);
	public bool IsAutoRange
	{
		get => _isAutoRange;
		set {
			if (_isAutoRange == value)
				return;
			_isAutoRange = value;
			Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
		}
	}

	private bool _isIntensityGrid = false;
	public static readonly DirectProperty<GraphControl, bool> IsIntensityGridProperty =
		AvaloniaProperty.RegisterDirect<GraphControl, bool>(
			nameof(IsIntensityGrid),
			o => o.IsIntensityGrid,
			(o, v) => o.IsIntensityGrid = v
		);
	public bool IsIntensityGrid
	{
		get => _isIntensityGrid;
		set {
			if (_isIntensityGrid == value)
				return;
			_isIntensityGrid = value;
			Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
		}
	}

	private Dictionary<SKColor, float[]>? _data;
	public static readonly DirectProperty<GraphControl, Dictionary<SKColor, float[]>?> DataProperty =
		AvaloniaProperty.RegisterDirect<GraphControl, Dictionary<SKColor, float[]>?>(
			nameof(Data),
			o => o.Data,
			(o, v) => o.Data = v
		);
	public Dictionary<SKColor, float[]>? Data
	{
		get => _data;
		set {
			if (_data == value)
				return;
			_data = value;
			Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
		}
	}

	public GraphControl()
	{
		ClipToBounds = true;
	}

	public override void Render(DrawingContext context) => context.Custom(this);

	public bool Equals(ICustomDrawOperation? other) => false;
	public bool HitTest(Point p) => true;
	public void Dispose() => GC.SuppressFinalize(this);

	SKPaint EdgePaint { get; set; } = new SKPaint
	{
		Style = SKPaintStyle.Fill,
		StrokeWidth = 2,
		IsAntialias = true,
		Typeface = KyoshinEewViewerFonts.MainRegular,
		TextSize = 14,
	};
	SKPaint GridPaint { get; set; } = new SKPaint
	{
		Style = SKPaintStyle.Fill,
		StrokeWidth = 1,
		IsAntialias = true,
	};
	SKPaint BodyPaint { get; set; } = new SKPaint
	{
		Style = SKPaintStyle.Fill,
		StrokeWidth = 1,
		IsAntialias = true,
	};
	readonly SKPathEffect _overPathEffect = SKPathEffect.CreateDash(new[] { 4f, 2f }, 2);

	public void UpdateResources()
	{
		SKColor FindColorResource(string name)
			=> ((Color)(this.FindResource(name) ?? throw new Exception($"マップリソース {name} が見つかりませんでした"))).ToSKColor();

		GridPaint.Color = EdgePaint.Color = FindColorResource("SubForegroundColor");
	}


	public void Render(ImmediateDrawingContext context)
	{
		if (!context.TryGetFeature<ISkiaSharpApiLeaseFeature>(out var leaseFeature))
			return;
		using var lease = leaseFeature.Lease();
		var canvas = lease.SkCanvas;

		float verticalHeaderSize = IsIntensityGrid ? 15 : 40;

		canvas.Save();
		try
		{
			// スケール計算
			var graphTop = MaxValue;
			var graphBottom = MinValue;

			if (IsAutoRange && Data != null)
			{
				var t = Data.Select(d => d.Value.Max()).Max();
				graphTop = Math.Max(MaxValue, t + 10 - (t % 10));
				var b = Data.Select(d => d.Value.Min()).Min();
				graphBottom = Math.Min(MinValue, b - (b % 10));

				var tbm = Math.Max(Math.Abs(graphTop), Math.Abs(graphBottom));
				graphTop = tbm;
				graphBottom = -tbm;
			}

			// 左ヘッダ部分
			canvas.DrawLine(new SKPoint(verticalHeaderSize, 0), new SKPoint(verticalHeaderSize, (float)Bounds.Height), EdgePaint);
			var gtText = ((int)graphTop).ToString();
			canvas.DrawText(gtText, new SKPoint(verticalHeaderSize * .9f - EdgePaint.MeasureText(gtText), EdgePaint.TextSize), EdgePaint);
			var gbText = ((int)graphBottom).ToString();
			canvas.DrawText(gbText, new SKPoint(verticalHeaderSize * .9f - EdgePaint.MeasureText(gbText), (float)Bounds.Height), EdgePaint);
			// 原点
			if (graphTop >= 0 && graphBottom <= 0)
			{
				var h = graphTop / (graphTop - graphBottom) * Bounds.Height;
				canvas.DrawLine(new SKPoint(verticalHeaderSize, (float)h), new SKPoint((float)Bounds.Width, (float)h), EdgePaint);
				if (graphTop != 0 && graphBottom != 0)
					canvas.DrawText("0".ToString(), new SKPoint(verticalHeaderSize * .9f - EdgePaint.MeasureText("0".ToString()), (float)(h + EdgePaint.TextSize / 2)), EdgePaint);
			}

			if (IsIntensityGrid)
				foreach (var v in new[] { 0.5, 1.5, 2.5, 3.5, 4.5, 5.0, 5.5, 6.0, 6.5 })
				{
					var h = (float)((graphTop - v) / (graphTop - graphBottom) * Bounds.Height);
					canvas.DrawLine(new SKPoint(verticalHeaderSize, (float)h), new SKPoint((float)Bounds.Width, (float)h), GridPaint);
					//canvas.DrawText(v.ToString(), new SKPoint(VerticalHeaderSize * .9f, (float)(h + EdgePaint.TextSize / 2)), EdgePaint);
				}
			//else
			//{
			//	// グリッド描画
			//	var gridSpan = (graphTop - graphBottom) / 10;
			//	for (var i = 0; i < 10; i++)
			//	{
			//		var c = graphBottom + gridSpan * i;
			//		var h = c / (graphTop - graphBottom) * Bounds.Height;
			//		if (c != 0)
			//			canvas.DrawLine(new(VerticalHeaderSize, (float)h), new((float)Bounds.Width, (float)h), GridPaint);
			//	}
			//}

			// グラフ本体描画
			if (Data == null || Data.Count <= 0)
				return;
			var len = Data.First().Value.Length;
			// 1点の横幅
			var step = (Bounds.Width - verticalHeaderSize) / len;
			foreach (var item in Data)
			{
				BodyPaint.Color = item.Key;
				var befPoint = new SKPoint();
				for (var i = 0; i < item.Value.Length && i < len; i++)
				{
					var p = new SKPoint((float)(verticalHeaderSize + i * step), (float)((graphTop - item.Value[i]) / (graphTop - graphBottom) * Bounds.Height));

					if (i > 0)
					{
						if (item.Value[i] >= graphTop)
						{
							p = new SKPoint(p.X, 0);
							BodyPaint.Color = BodyPaint.Color.WithAlpha(50);
							canvas.DrawLine(befPoint, p, BodyPaint);
							BodyPaint.Color = BodyPaint.Color.WithAlpha(255);
						}
						else
							canvas.DrawLine(befPoint, p, BodyPaint);
					}
					befPoint = p;
				}
			}
		}
		finally
		{
			canvas.Restore();
		}
	}
}
