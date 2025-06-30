using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using KyoshinMonitorLib;
using SkiaSharp;
using System;
using System.Linq;

namespace KyoshinEewViewer.Series.ObservationPointEditor.Controls;

public class KyoshinImageMapCanvas : Control, ICustomDrawOperation
{
	#region 依存関係プロパティ

	public static readonly DirectProperty<KyoshinImageMapCanvas, Point> CenterPointProperty =
		AvaloniaProperty.RegisterDirect<KyoshinImageMapCanvas, Point>(
			nameof(CenterPoint),
			o => o.CenterPoint,
			(o, v) => o.CenterPoint = v,
			new Point(176, 200));

	private Point _centerPoint = new(176, 200);
	public Point CenterPoint
	{
		get => _centerPoint;
		set => SetAndRaise(CenterPointProperty, ref _centerPoint, value);
	}

	public static readonly DirectProperty<KyoshinImageMapCanvas, double> ScaleProperty =
		AvaloniaProperty.RegisterDirect<KyoshinImageMapCanvas, double>(
			nameof(Scale),
			o => o.Scale,
			(o, v) => o.Scale = v,
			1.0);

	private double _scale = 1.0;
	public double Scale
	{
		get => _scale;
		set => SetAndRaise(ScaleProperty, ref _scale, value);
	}

	public static readonly DirectProperty<KyoshinImageMapCanvas, ObservationPoint[]> ObservationPointsProperty =
		AvaloniaProperty.RegisterDirect<KyoshinImageMapCanvas, ObservationPoint[]>(
			nameof(ObservationPoints),
			o => o.ObservationPoints,
			(o, v) => o.ObservationPoints = v,
			[]);

	private ObservationPoint[] _observationPoints = [];
	public ObservationPoint[] ObservationPoints
	{
		get => _observationPoints;
		set => SetAndRaise(ObservationPointsProperty, ref _observationPoints, value);
	}

	public static readonly DirectProperty<KyoshinImageMapCanvas, ObservationPoint?> SelectedObservationPointProperty =
		AvaloniaProperty.RegisterDirect<KyoshinImageMapCanvas, ObservationPoint?>(
			nameof(SelectedObservationPoint),
			o => o.SelectedObservationPoint,
			(o, v) => o.SelectedObservationPoint = v);

	private ObservationPoint? _selectedObservationPoint;
	public ObservationPoint? SelectedObservationPoint
	{
		get => _selectedObservationPoint;
		set => SetAndRaise(SelectedObservationPointProperty, ref _selectedObservationPoint, value);
	}



	public static readonly DirectProperty<KyoshinImageMapCanvas, SKBitmap?> BackgroundImageProperty =
		AvaloniaProperty.RegisterDirect<KyoshinImageMapCanvas, SKBitmap?>(
			nameof(BackgroundImage),
			o => o.BackgroundImage,
			(o, v) => o.BackgroundImage = v);

	private SKBitmap? _backgroundImage;
	public SKBitmap? BackgroundImage
	{
		get => _backgroundImage;
		set => SetAndRaise(BackgroundImageProperty, ref _backgroundImage, value);
	}

	public static readonly DirectProperty<KyoshinImageMapCanvas, SKBitmap?> KyoshinImageProperty =
		AvaloniaProperty.RegisterDirect<KyoshinImageMapCanvas, SKBitmap?>(
			nameof(KyoshinImage),
			o => o.KyoshinImage,
			(o, v) => o.KyoshinImage = v);

	private SKBitmap? _kyoshinImage;
	public SKBitmap? KyoshinImage
	{
		get => _kyoshinImage;
		set => SetAndRaise(KyoshinImageProperty, ref _kyoshinImage, value);
	}

	public static readonly DirectProperty<KyoshinImageMapCanvas, bool> ShowMonitorImageProperty =
		AvaloniaProperty.RegisterDirect<KyoshinImageMapCanvas, bool>(
			nameof(ShowMonitorImage),
			o => o.ShowMonitorImage,
			(o, v) => o.ShowMonitorImage = v,
			true);

	private bool _showMonitorImage = true;
	public bool ShowMonitorImage
	{
		get => _showMonitorImage;
		set => SetAndRaise(ShowMonitorImageProperty, ref _showMonitorImage, value);
	}

	public static readonly DirectProperty<KyoshinImageMapCanvas, bool> ShowObservationPointsProperty =
		AvaloniaProperty.RegisterDirect<KyoshinImageMapCanvas, bool>(
			nameof(ShowObservationPoints),
			o => o.ShowObservationPoints,
			(o, v) => o.ShowObservationPoints = v,
			true);

	private bool _showObservationPoints = true;
	public bool ShowObservationPoints
	{
		get => _showObservationPoints;
		set => SetAndRaise(ShowObservationPointsProperty, ref _showObservationPoints, value);
	}

	#endregion

	#region イベント

	public event EventHandler<ObservationPointMovedEventArgs>? ObservationPointMoved;

	#endregion

	#region プライベートフィールド

	private Point? _previousMousePoint;
	private bool _isRightDragging;

	private const double BaseImageWidth = 352;
	private const double BaseImageHeight = 400;

	#endregion

	public KyoshinImageMapCanvas()
	{
		PropertyChanged += OnPropertyChanged;
		
		// フォーカス可能にする
		Focusable = true;
		
		// クリック時にフォーカスを取得
		PointerPressed += (_, _) => Focus();
	}

	private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
	{
		if (e.Property == ScaleProperty ||
			e.Property == CenterPointProperty ||
			e.Property == ObservationPointsProperty ||
			e.Property == SelectedObservationPointProperty ||
			e.Property == BackgroundImageProperty ||
			e.Property == KyoshinImageProperty ||
			e.Property == ShowMonitorImageProperty ||
			e.Property == ShowObservationPointsProperty)
		{
			InvalidateVisual();
		}
	}

	#region 描画処理

	public override void Render(DrawingContext context)
	{
		base.Render(context);

		if (!IsVisible) return;

		context.Custom(this);
	}

	public bool HitTest(Point p) => true;

	public void Render(ImmediateDrawingContext context)
	{
		if (!context.TryGetFeature<ISkiaSharpApiLeaseFeature>(out var leaseFeature))
			return;

		using var lease = leaseFeature.Lease();
		var canvas = lease.SkCanvas;

		var renderSize = Bounds.Size;
		var scale = Scale;
		var halfRenderSize = new Vector(renderSize.Width / 2, renderSize.Height / 2);
		var offset = new Vector(CenterPoint.X * scale, CenterPoint.Y * scale) - halfRenderSize;

		canvas.Save();
		try
		{
			// 背景画像の描画
			if (BackgroundImage != null)
			{
				var imageRect = SKRect.Create(
					(float)-offset.X, (float)-offset.Y,
					(float)(BaseImageWidth * scale),
					(float)(BaseImageHeight * scale));
				canvas.DrawBitmap(BackgroundImage, imageRect);
			}

			// 強震画像の描画
			if (KyoshinImage != null && ShowMonitorImage)
			{
				var imageRect = SKRect.Create(
					(float)-offset.X, (float)-offset.Y,
					(float)(BaseImageWidth * scale),
					(float)(BaseImageHeight * scale));
				canvas.DrawBitmap(KyoshinImage, imageRect);
			}

			// 観測点の描画
			if (ObservationPoints != null && ShowObservationPoints)
				DrawObservationPoints(canvas, scale, offset, renderSize);
		}
		finally
		{
			canvas.Restore();

			if (IsFocused)
				canvas.DrawRect(
					new SKRect(0, 0, (float)renderSize.Width, (float)renderSize.Height),
					new SKPaint { Color = SKColors.Red, Style = SKPaintStyle.Stroke, StrokeWidth = 5 });
		}
	}

	public void Dispose() => GC.SuppressFinalize(this);
	public bool Equals(ICustomDrawOperation? other) => false;

	private void DrawObservationPoints(SKCanvas canvas, double scale, Vector offset, Size renderSize)
	{
		if (ObservationPoints == null) return;

		var displayRect = new Rect(
			CenterPoint.X - renderSize.Width / 2 / scale,
			CenterPoint.Y - renderSize.Height / 2 / scale,
			renderSize.Width / scale,
			renderSize.Height / scale);

		foreach (var point in ObservationPoints)
		{
			if (!point.Point.HasValue) continue;
			var pixelPoint = point.Point.Value;

			// 表示範囲外の場合はスキップ
			if (!displayRect.Contains(new Point(pixelPoint.X, pixelPoint.Y))) continue;
			// 画面座標に変換
			var screenX = pixelPoint.X * scale - offset.X;
			var screenY = pixelPoint.Y * scale - offset.Y;

			// 観測点の描画
			DrawObservationPoint(canvas, point, screenX, screenY, scale);
		}
	}

	private void DrawObservationPoint(SKCanvas canvas, ObservationPoint point, double x, double y, double scale)
	{
		var rect = SKRect.Create((float)x, (float)y, (float)scale, (float)scale);

		// 観測点種類による色分け
		var fillColor = point.Type switch
		{
			ObservationPointType.KiK_net => SKColors.Red,
			ObservationPointType.K_NET => SKColors.Orange,
			_ => SKColors.DimGray,
		};

		if (point.IsSuspended)
			fillColor = SKColors.Gray;

		using var fillPaint = new SKPaint { Color = fillColor, Style = SKPaintStyle.Fill };
		canvas.DrawRect(rect, fillPaint);

		// 選択状態の枠線
		if (point == SelectedObservationPoint)
		{
			using var borderPaint = new SKPaint { Color = SKColors.Magenta, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
			canvas.DrawRect(rect, borderPaint);
		}

	}

	#endregion

	#region マウス・ポインタ操作

	protected override void OnPointerPressed(PointerPressedEventArgs e)
	{
		base.OnPointerPressed(e);

		var position = e.GetPosition(this);
		_previousMousePoint = position;

		var properties = e.GetCurrentPoint(this).Properties;

		if (properties.IsLeftButtonPressed)
		{
			// 左ボタン：選択中観測点の座標設定
			if (SelectedObservationPoint != null)
			{
				var imagePosition = ScreenToImagePosition(position);
				var newPoint = new Point2((int)Math.Round(imagePosition.X), (int)Math.Round(imagePosition.Y));
				OnObservationPointMoved(SelectedObservationPoint, newPoint);
			}
		}
		else if (properties.IsRightButtonPressed)
		{
			// 右ボタン：パン開始
			_isRightDragging = true;
		}
		else if (properties.IsMiddleButtonPressed)
		{
			// 中ボタン：観測点選択
			var clickedPoint = GetObservationPointAt(position);
			if (clickedPoint != null)
			{
				SelectedObservationPoint = clickedPoint;
			}
		}
	}

	protected override void OnPointerMoved(PointerEventArgs e)
	{
		base.OnPointerMoved(e);

		var position = e.GetPosition(this);

		if (_previousMousePoint == null) return;

		var properties = e.GetCurrentPoint(this).Properties;

		if (properties.IsRightButtonPressed && _isRightDragging)
		{
			// 右ドラッグ：パン操作
			var delta = position - _previousMousePoint.Value;
			var newCenter = CenterPoint - new Point(delta.X / Scale, delta.Y / Scale);

			// 境界チェック
			newCenter = new Point(
				Math.Max(0, Math.Min(BaseImageWidth, newCenter.X)),
				Math.Max(0, Math.Min(BaseImageHeight, newCenter.Y)));

			CenterPoint = newCenter;
		}

		_previousMousePoint = position;
	}

	protected override void OnPointerReleased(PointerReleasedEventArgs e)
	{
		base.OnPointerReleased(e);

		_isRightDragging = false;
		_previousMousePoint = null;
	}

	protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
	{
		base.OnPointerWheelChanged(e);

		// マウス位置を中心としたズーム
		var mousePosition = e.GetPosition(this);
		var mouseImagePosition = ScreenToImagePosition(mousePosition);
		
		var delta = e.Delta.Y;
		var newScale = Math.Max(1, Math.Min(10.0, Scale + delta * 0.5));
		
		// ズーム後もマウス位置の画像座標が同じ場所に来るようにCenterPointを調整
		var renderSize = Bounds.Size;
		var halfRenderSize = new Vector(renderSize.Width / 2, renderSize.Height / 2);
		var mouseOffset = new Vector(mousePosition.X, mousePosition.Y) - halfRenderSize;
		
		var newCenterPoint = mouseImagePosition - mouseOffset / newScale;
		
		// 境界チェック
		newCenterPoint = new Point(
			Math.Max(0, Math.Min(BaseImageWidth, newCenterPoint.X)),
			Math.Max(0, Math.Min(BaseImageHeight, newCenterPoint.Y)));
		
		Scale = newScale;
		CenterPoint = newCenterPoint;
	}

	#endregion

	#region ヘルパーメソッド

	private ObservationPoint? GetObservationPointAt(Point screenPosition)
	{
		if (ObservationPoints == null) return null;

		var imagePosition = ScreenToImagePosition(screenPosition);
		var pixelX = (int)Math.Floor(imagePosition.X);
		var pixelY = (int)Math.Floor(imagePosition.Y);

		return ObservationPoints
			.Where(p => p.Point.HasValue)
			.FirstOrDefault(p =>
				p.Point!.Value.X == pixelX &&
				p.Point!.Value.Y == pixelY);
	}

	private Point ScreenToImagePosition(Point screenPosition)
	{
		var renderSize = Bounds.Size;
		var scale = Scale;
		var halfRenderSize = new Vector(renderSize.Width / 2, renderSize.Height / 2);
		var offset = new Vector(CenterPoint.X * scale, CenterPoint.Y * scale) - halfRenderSize;

		return new Point(
			(screenPosition.X + offset.X) / scale,
			(screenPosition.Y + offset.Y) / scale);
	}

	public Point GetMouseImagePosition(Point screenPosition) => ScreenToImagePosition(screenPosition);

	#endregion

	#region 保護されたイベント発火メソッド


	protected virtual void OnObservationPointMoved(ObservationPoint point, Point2 newPosition) =>
		ObservationPointMoved?.Invoke(this, new ObservationPointMovedEventArgs(point, newPosition));

	#endregion
}

#region イベント引数クラス

public class ObservationPointClickedEventArgs(ObservationPoint? point, PointerButton button, Point2? newPosition = null) : EventArgs
{
	public ObservationPoint? ObservationPoint { get; } = point;
	public PointerButton Button { get; } = button;
	public Point2? NewPosition { get; } = newPosition;
}

public class ObservationPointMovedEventArgs(ObservationPoint point, Point2 newPosition) : EventArgs
{
	public ObservationPoint ObservationPoint { get; } = point;
	public Point2 NewPosition { get; } = newPosition;
}

#endregion

public enum PointerButton
{
	Left,
	Right,
	Middle
}
