using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using KyoshinMonitorLib;
using KyoshinMonitorLib.UrlGenerator;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.ObservationPointEditor.Controls;

public partial class KyoshinImageMapControl : UserControl, ICustomDrawOperation
{
	#region 依存関係プロパティ

	public static readonly StyledProperty<Point> CenterPointProperty =
		AvaloniaProperty.Register<KyoshinImageMapControl, Point>(nameof(CenterPoint), new Point(176, 200));

	public Point CenterPoint
	{
		get => GetValue(CenterPointProperty);
		set => SetValue(CenterPointProperty, value);
	}

	public static readonly StyledProperty<double> ScaleProperty =
		AvaloniaProperty.Register<KyoshinImageMapControl, double>(nameof(Scale), 1.0);

	public double Scale
	{
		get => GetValue(ScaleProperty);
		set => SetValue(ScaleProperty, value);
	}

	public static readonly StyledProperty<ObservationPoint[]?> ObservationPointsProperty =
		AvaloniaProperty.Register<KyoshinImageMapControl, ObservationPoint[]?>(nameof(ObservationPoints));

	public ObservationPoint[]? ObservationPoints
	{
		get => GetValue(ObservationPointsProperty);
		set => SetValue(ObservationPointsProperty, value);
	}

	public static readonly StyledProperty<ObservationPoint?> SelectedObservationPointProperty =
		AvaloniaProperty.Register<KyoshinImageMapControl, ObservationPoint?>(nameof(SelectedObservationPoint));

	public ObservationPoint? SelectedObservationPoint
	{
		get => GetValue(SelectedObservationPointProperty);
		set => SetValue(SelectedObservationPointProperty, value);
	}

	public static readonly StyledProperty<bool> ShowDebugInfoProperty =
		AvaloniaProperty.Register<KyoshinImageMapControl, bool>(nameof(ShowDebugInfo), false);

	public bool ShowDebugInfo
	{
		get => GetValue(ShowDebugInfoProperty);
		set => SetValue(ShowDebugInfoProperty, value);
	}

	#endregion

	#region イベント

	public event EventHandler<ObservationPointClickedEventArgs>? ObservationPointClicked;
	public event EventHandler<ObservationPointMovedEventArgs>? ObservationPointMoved;

	#endregion

	#region プライベートフィールド

	private readonly HttpClient _httpClient = new();
	private SKBitmap? _backgroundImage;
	private SKBitmap? _kyoshinImage;
	private RealtimeDataType _currentImageType = RealtimeDataType.Shindo;
	
	private Point? _mouseDownPoint;
	private Point? _previousMousePoint;
	private ObservationPoint? _draggingPoint;
	private bool _isDragging;

	private const double BaseImageWidth = 352;
	private const double BaseImageHeight = 400;

	#endregion

	public KyoshinImageMapControl()
	{
		InitializeComponent();
		InitializeControls();
		LoadBackgroundImage();
		UpdateKyoshinImage();
	}

	private void InitializeControls()
	{
		// 画像種類コンボボックスの初期化
		foreach (var dataType in Enum.GetValues<RealtimeDataType>())
		{
			ImageTypeComboBox.Items.Add(dataType);
		}
		ImageTypeComboBox.SelectedItem = RealtimeDataType.Shindo;

		// スケールスライダーの初期化
		ScaleSlider.Value = Scale;
		UpdateScaleText();

		// プロパティ変更の監視
		this.PropertyChanged += OnPropertyChanged;
	}

	private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
	{
		if (e.Property == ScaleProperty)
		{
			UpdateScaleText();
			InvalidateVisual();
		}
		else if (e.Property == CenterPointProperty ||
		         e.Property == ObservationPointsProperty ||
		         e.Property == SelectedObservationPointProperty)
		{
			InvalidateVisual();
		}
	}

	private void UpdateScaleText()
	{
		ScaleText.Text = $"x{Scale:F1}";
	}

	#region 画像読み込み

	private async void LoadBackgroundImage()
	{
		try
		{
			var backgroundUrl = "http://www.kmoni.bosai.go.jp/data/map_img/CommonImg/base_map_w.gif";
			var imageData = await _httpClient.GetByteArrayAsync(backgroundUrl);
			_backgroundImage = SKBitmap.Decode(imageData);
			InvalidateVisual();
		}
		catch (Exception ex)
		{
			// エラーハンドリング（ログ出力など）
			System.Diagnostics.Debug.WriteLine($"背景画像の読み込みに失敗: {ex.Message}");
		}
	}

	private async void UpdateKyoshinImage()
	{
		try
		{
			var imageUrl = WebApiUrlGenerator.Generate(
				WebApiUrlType.RealtimeImg, 
				DateTime.Now.AddMinutes(-1), 
				_currentImageType);
			
			var imageData = await _httpClient.GetByteArrayAsync(imageUrl);
			_kyoshinImage = SKBitmap.Decode(imageData);
			InvalidateVisual();
		}
		catch (Exception ex)
		{
			// エラーハンドリング（ログ出力など）
			System.Diagnostics.Debug.WriteLine($"強震画像の読み込みに失敗: {ex.Message}");
		}
	}

	#endregion

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

		// 背景画像の描画
		if (_backgroundImage != null)
		{
			var imageRect = SKRect.Create(
				(float)(-offset.X), (float)(-offset.Y),
				(float)(BaseImageWidth * scale), 
				(float)(BaseImageHeight * scale));
			canvas.DrawBitmap(_backgroundImage, imageRect);
		}

		// 強震画像の描画
		if (_kyoshinImage != null && ShowMonitorImageCheckBox.IsChecked == true)
		{
			var imageRect = SKRect.Create(
				(float)(-offset.X), (float)(-offset.Y),
				(float)(BaseImageWidth * scale), 
				(float)(BaseImageHeight * scale));
			canvas.DrawBitmap(_kyoshinImage, imageRect);
		}

		// 観測点の描画
		if (ObservationPoints != null && ShowObservationPointCheckBox.IsChecked == true)
		{
			DrawObservationPoints(canvas, scale, offset, renderSize);
		}
	}

	public void Dispose() => GC.SuppressFinalize(this);
	public bool Equals(ICustomDrawOperation? other) => false;

	private void DrawObservationPoints(SKCanvas canvas, double scale, Vector offset, Size renderSize)
	{
		if (ObservationPoints == null) return;

		var displayRect = new Rect(
			(CenterPoint.X - renderSize.Width / 2 / scale),
			(CenterPoint.Y - renderSize.Height / 2 / scale),
			renderSize.Width / scale,
			renderSize.Height / scale);

		foreach (var point in ObservationPoints)
		{
			if (!point.Point.HasValue) continue;

			var pixelPoint = point.Point.Value;
			
			// 表示範囲外の場合はスキップ
			if (!displayRect.Contains(new Avalonia.Point(pixelPoint.X, pixelPoint.Y))) continue;

			// 画面座標に変換
			var screenX = pixelPoint.X * scale - offset.X;
			var screenY = pixelPoint.Y * scale - offset.Y;

			// 観測点の描画
			DrawObservationPoint(canvas, point, screenX, screenY, scale);
		}
	}

	private void DrawObservationPoint(SKCanvas canvas, ObservationPoint point, double x, double y, double scale)
	{
		var size = (float)Math.Max(2, scale * 0.8);
		var rect = SKRect.Create((float)(x - size/2), (float)(y - size/2), size, size);

		// 観測点種類による色分け
		SKColor fillColor = point.Type switch
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

		// ドラッグ中の場合は透明度を下げる
		if (_isDragging && point == _draggingPoint)
		{
			var dragColor = SKColors.Yellow.WithAlpha(128);
			using var dragFillPaint = new SKPaint { Color = dragColor, Style = SKPaintStyle.Fill };
			using var dragBorderPaint = new SKPaint { Color = SKColors.Yellow, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
			canvas.DrawRect(rect, dragFillPaint);
			canvas.DrawRect(rect, dragBorderPaint);
		}
	}

	#endregion

	#region マウス・ポインタ操作

	private void Canvas_PointerPressed(object? sender, PointerPressedEventArgs e)
	{
		var position = e.GetPosition(RenderArea);
		_mouseDownPoint = _previousMousePoint = position;

		var properties = e.GetCurrentPoint(RenderArea).Properties;
		
		if (properties.IsLeftButtonPressed)
		{
			// 左ボタン：選択またはドラッグ開始
			var clickedPoint = GetObservationPointAt(position);
			if (clickedPoint != null)
			{
				SelectedObservationPoint = clickedPoint;
				_draggingPoint = clickedPoint;
				_isDragging = true;
				OnObservationPointClicked(clickedPoint, PointerButton.Left);
			}
			else
			{
				// 空白部分をクリック：パン開始
				SelectedObservationPoint = null;
			}
		}
		else if (properties.IsRightButtonPressed)
		{
			// 右ボタン：削除
			var clickedPoint = GetObservationPointAt(position);
			if (clickedPoint != null)
			{
				OnObservationPointClicked(clickedPoint, PointerButton.Right);
			}
		}
		else if (properties.IsMiddleButtonPressed)
		{
			// 中ボタン：新規追加
			var imagePosition = ScreenToImagePosition(position);
			OnObservationPointClicked(null, PointerButton.Middle, new KyoshinMonitorLib.Point2((int)imagePosition.X, (int)imagePosition.Y));
		}

		// AvaloniaUI 11ではポインタキャプチャは不要
		// ドラッグ操作はPointerMovedイベントで直接処理
	}

	private void Canvas_PointerMoved(object? sender, PointerEventArgs e)
	{
		var position = e.GetPosition(RenderArea);

		// マウス位置情報の更新
		var imagePos = ScreenToImagePosition(position);
		MousePositionText.Text = $"マウス位置: ({imagePos.X:F0}, {imagePos.Y:F0})";

		if (_previousMousePoint == null) return;

		var properties = e.GetCurrentPoint(RenderArea).Properties;

		if (properties.IsLeftButtonPressed && _isDragging && _draggingPoint != null)
		{
			// 観測点のドラッグ
			var imagePosition = ScreenToImagePosition(position);
			var newPoint = new Point2((int)Math.Round(imagePosition.X), (int)Math.Round(imagePosition.Y));
			
			OnObservationPointMoved(_draggingPoint, newPoint);
		}
		else if (properties.IsLeftButtonPressed && !_isDragging)
		{
			// パン操作
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

	private void Canvas_PointerReleased(object? sender, PointerReleasedEventArgs e)
	{
		_isDragging = false;
		_draggingPoint = null;
		_mouseDownPoint = null;
		_previousMousePoint = null;
		
		// ポインタキャプチャ解放は不要
	}

	private void Canvas_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
	{
		var delta = e.Delta.Y;
		var newScale = Math.Max(ScaleSlider.Minimum, 
						Math.Min(ScaleSlider.Maximum, 
						Scale + delta * 0.1));
		
		Scale = newScale;
		ScaleSlider.Value = newScale;
	}

	#endregion

	#region ヘルパーメソッド

	private ObservationPoint? GetObservationPointAt(Point screenPosition)
	{
		if (ObservationPoints == null) return null;

		var imagePosition = ScreenToImagePosition(screenPosition);
		const double tolerance = 5.0; // ピクセル

		return ObservationPoints
			.Where(p => p.Point.HasValue)
			.FirstOrDefault(p =>
			{
				var distance = Math.Sqrt(
					Math.Pow(p.Point!.Value.X - imagePosition.X, 2) +
					Math.Pow(p.Point!.Value.Y - imagePosition.Y, 2));
				return distance <= tolerance;
			});
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

	#endregion

	#region イベントハンドラ

	private void ScaleSlider_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
	{
		Scale = e.NewValue;
	}

	private void RefreshImageButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
	{
		UpdateKyoshinImage();
	}

	private void ImageTypeComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (ImageTypeComboBox.SelectedItem is RealtimeDataType dataType)
		{
			_currentImageType = dataType;
			UpdateKyoshinImage();
		}
	}

	private void ShowOption_Changed(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
	{
		InvalidateVisual();
	}

	#endregion

	#region 保護されたイベント発火メソッド

	protected virtual void OnObservationPointClicked(ObservationPoint? point, PointerButton button, Point2? newPosition = null)
	{
		ObservationPointClicked?.Invoke(this, new ObservationPointClickedEventArgs(point, button, newPosition));
	}

	protected virtual void OnObservationPointMoved(ObservationPoint point, Point2 newPosition)
	{
		ObservationPointMoved?.Invoke(this, new ObservationPointMovedEventArgs(point, newPosition));
	}

	#endregion

	protected override void OnUnloaded(Avalonia.Interactivity.RoutedEventArgs e)
	{
		base.OnUnloaded(e);
		_httpClient?.Dispose();
		_backgroundImage?.Dispose();
		_kyoshinImage?.Dispose();
	}
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