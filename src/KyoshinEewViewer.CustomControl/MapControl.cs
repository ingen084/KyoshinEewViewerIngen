using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Metrics;
using KyoshinEewViewer.CustomControl.Manipulations;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Layers;
using KyoshinMonitorLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace KyoshinEewViewer.CustomControl;

public class MapControl : Avalonia.Controls.Control, ICustomDrawOperation
{
	private Location _centerLocation = new(36.474f, 135.264f);
	public static readonly DirectProperty<MapControl, Location> CenterLocationProperty =
		AvaloniaProperty.RegisterDirect<MapControl, Location>(
			nameof(CenterLocation),
			o => o.CenterLocation,
			(o, v) => o.CenterLocation = v
		);
	public Location CenterLocation
	{
		get => _centerLocation;
		set {
			if (!SetAndRaise(CenterLocationProperty, ref _centerLocation, value))
				return;
			if (_centerLocation != null)
			{
				var cl = _centerLocation;
				cl.Latitude = Math.Min(Math.Max(cl.Latitude, -80), 80);
				// 1回転させる
				if (cl.Longitude < -180)
					cl.Longitude += 360;
				if (cl.Longitude > 180)
					cl.Longitude -= 360;
				_centerLocation = cl;
			}

			Dispatcher.UIThread.Post(() =>
			{
				ApplySize();
				InvalidateVisual();
			});
		}
	}

	private double _zoom = 4;
	public static readonly DirectProperty<MapControl, double> ZoomProperty =
		AvaloniaProperty.RegisterDirect<MapControl, double>(
			nameof(Zoom),
			o => o.Zoom,
			(o, v) => o.Zoom = v
		);
	public double Zoom
	{
		get => _zoom;
		set {
			var fb = Math.Min(Math.Max(value, MinZoom), MaxZoom);
			if (!SetAndRaise(ZoomProperty, ref _zoom, fb))
				return;
			Dispatcher.UIThread.Post(() =>
			{
				ApplySize();
				InvalidateVisual();
			});
		}
	}

	private MapLayerHost LayerHost { get; } = new();

	/// <summary>
	/// 最新の描画パフォーマンスメトリクス
	/// </summary>
	private FrameRenderMetrics? _latestMetrics;
	public FrameRenderMetrics? LatestMetrics
	{
		get => _latestMetrics;
		private set => SetAndRaise(LatestMetricsProperty, ref _latestMetrics, value);
	}
	public static readonly DirectProperty<MapControl, FrameRenderMetrics?> LatestMetricsProperty =
		AvaloniaProperty.RegisterDirect<MapControl, FrameRenderMetrics?>(
			nameof(LatestMetrics),
			o => o.LatestMetrics);

	private bool _isMetricsEnabled;
	public static readonly DirectProperty<MapControl, bool> IsMetricsEnabledProperty =
		AvaloniaProperty.RegisterDirect<MapControl, bool>(
			nameof(IsMetricsEnabled),
			o => o.IsMetricsEnabled,
			(o, v) => o.IsMetricsEnabled = v
		);
	/// <summary>
	/// パフォーマンスメトリクスの収集を有効にするかどうか
	/// </summary>
	public bool IsMetricsEnabled
	{
		get => _isMetricsEnabled;
		set => SetAndRaise(IsMetricsEnabledProperty, ref _isMetricsEnabled, value);
	}

	private DateTime _lastMetricsRecordTime = DateTime.MinValue;
	private static readonly TimeSpan MetricsRecordInterval = TimeSpan.FromSeconds(0.5);

	public static readonly DirectProperty<MapControl, MapLayer[]?> LayersProperty =
		AvaloniaProperty.RegisterDirect<MapControl, MapLayer[]?>(
			nameof(Layers),
			o => o.Layers,
			(o, v) => o.Layers = v,
			null
		);
	public MapLayer[]? Layers
	{
		get => LayerHost.Layers;
		set => LayerHost.Layers = value;
	}

	private double _maxZoom = 12;
	public static readonly DirectProperty<MapControl, double> MaxZoomProperty =
		AvaloniaProperty.RegisterDirect<MapControl, double>(
			nameof(MaxZoom),
			o => o.MaxZoom,
			(o, v) => o.MaxZoom = v
		);
	public double MaxZoom
	{
		get => _maxZoom;
		set {
			SetAndRaise(MaxZoomProperty, ref _maxZoom, value);
			Zoom = _zoom;
		}
	}

	public static readonly DirectProperty<MapControl, double> MaxNavigateZoomProperty =
		AvaloniaProperty.RegisterDirect<MapControl, double>(
			nameof(MaxNavigateZoom),
			o => o.MaxNavigateZoom,
			(o, v) => o.MaxNavigateZoom = v);
	public double MaxNavigateZoom { get; set; } = 10;

	private double _minZoom = 4;
	public static readonly DirectProperty<MapControl, double> MinZoomProperty =
		AvaloniaProperty.RegisterDirect<MapControl, double>(
			nameof(MinZoom),
			o => o.MinZoom,
			(o, v) => o.MinZoom = v
		);
	public double MinZoom
	{
		get => _minZoom;
		set {
			SetAndRaise(MinZoomProperty, ref _minZoom, value);
			Zoom = _zoom;
		}
	}

	private Thickness _padding = new();
	public static readonly DirectProperty<MapControl, Thickness> PaddingProperty =
		AvaloniaProperty.RegisterDirect<MapControl, Thickness>(
			nameof(Padding),
			o => o.Padding,
			(o, v) => o.Padding = v
		);
	public Thickness Padding
	{
		get => _padding;
		set {
			SetAndRaise(PaddingProperty, ref _padding, value);

			Dispatcher.UIThread.Post(() =>
			{
				ApplySize();
				InvalidateVisual();
			});
		}
	}

	public static readonly DirectProperty<MapControl, bool> IsHeadlessModeProperty =
		AvaloniaProperty.RegisterDirect<MapControl, bool>(
			nameof(IsHeadlessMode),
			o => o.IsHeadlessMode,
			(o, v) => o.IsHeadlessMode = v
		);
	private bool _isHeadlessMode = false;
	public bool IsHeadlessMode
	{
		get => _isHeadlessMode;
		set => SetAndRaise(IsHeadlessModeProperty, ref _isHeadlessMode, value);
	}

	public static readonly DirectProperty<MapControl, bool> IsDisableManualControlProperty =
		AvaloniaProperty.RegisterDirect<MapControl, bool>(
			nameof(IsDisableManualControl),
			o => o.IsDisableManualControl,
			(o, v) => o.IsDisableManualControl = v
		);
	private bool _isDisableManualControl = false;
	public bool IsDisableManualControl
	{
		get => _isDisableManualControl;
		set => SetAndRaise(IsDisableManualControlProperty, ref _isDisableManualControl, value);
	}

	#region Navigate
	private NavigateAnimation? NavigateAnimation { get; set; }
	public bool IsNavigating => NavigateAnimation?.IsRunning ?? false;

	public void Navigate(Rect bound, TimeSpan duration, bool unlimitNavigateZoom = false)
		=> Navigate(new RectD(bound.X, bound.Y, bound.Width, bound.Height), duration, unlimitNavigateZoom);
	public void Navigate(Rect bound, TimeSpan duration, Rect mustBound)
		=> Navigate(new RectD(bound.X, bound.Y, bound.Width, bound.Height), duration, new RectD(mustBound.X, mustBound.Y, mustBound.Width, mustBound.Height));

	public void Navigate(RectD bound, TimeSpan duration, RectD mustBound)
	{
		var halfRenderSize = new PointD(PaddedRect.Width / 2, PaddedRect.Height / 2);
		// 左上/右下のピクセル座標
		var leftTop = (CenterLocation.ToPixel(Zoom) - halfRenderSize).ToLocation(Zoom);
		var rightBottom = (CenterLocation.ToPixel(Zoom) + halfRenderSize).ToLocation(Zoom);

		// 今見えている範囲よりmustBoundのほうがでかい場合ナビゲーションする
		if (mustBound.Left < rightBottom.Latitude || mustBound.Right > leftTop.Latitude ||
			mustBound.Top < leftTop.Longitude || mustBound.Bottom > rightBottom.Longitude || IsHeadlessMode)
			Navigate(bound, duration, false);
	}
	// 指定した範囲をすべて表示できるように調整する
	public void Navigate(RectD bound, TimeSpan duration, bool unlimitNavigateZoom = false)
	{
		if (!Dispatcher.UIThread.CheckAccess())
		{
			Dispatcher.UIThread.Post(() => Navigate(bound, duration, unlimitNavigateZoom));
			return;
		}
		var boundPixel = new RectD(bound.TopLeft.CastLocation().ToPixel(Zoom), bound.BottomRight.CastLocation().ToPixel(Zoom));
		var centerPixel = CenterLocation.ToPixel(Zoom);
		var halfRect = PaddedRect.Size / 2;
		var leftTop = centerPixel - halfRect;
		var rightBottom = centerPixel + halfRect;
		Navigate(new NavigateAnimation(
				Zoom,
				MinZoom,
				unlimitNavigateZoom ? MaxZoom : MaxNavigateZoom,
				new RectD(leftTop, rightBottom),
				boundPixel,
				duration,
				PaddedRect));
	}
	internal void Navigate(NavigateAnimation parameter)
	{
		if (PaddedRect.Width == 0 || PaddedRect.Height == 0)
			return;
		if (parameter.Duration <= TimeSpan.Zero)
		{
			(Zoom, CenterLocation) = parameter.GetCurrentParameter(Zoom, PaddedRect);
			Dispatcher.UIThread.Post(InvalidateVisual);
			return;
		}
		NavigateAnimation = parameter;
		NavigateAnimation.Start();
		Dispatcher.UIThread.Post(InvalidateVisual);
	}

	public bool IsNavigatedPosition(RectD bound)
	{
		var boundPixel = new RectD(bound.TopLeft.CastLocation().ToPixel(Zoom), bound.BottomRight.CastLocation().ToPixel(Zoom));
		var centerPixel = CenterLocation.ToPixel(Zoom);
		var halfRect = PaddedRect.Size / 2;
		var leftTop = centerPixel - halfRect;
		var rightBottom = centerPixel + halfRect;

		var anim = new NavigateAnimation(
				Zoom,
				MinZoom,
				MaxZoom,
				new RectD(leftTop, rightBottom),
				boundPixel,
				TimeSpan.Zero,
				PaddedRect);

		var (z, c) = anim.GetCurrentParameter(Zoom, PaddedRect);

		return Math.Abs(Zoom - z) < 0.001
			&& Math.Abs(c.Latitude - CenterLocation.Latitude) < 0.001
			&& Math.Abs(c.Longitude - CenterLocation.Longitude) < 0.001;
	}
	#endregion Navigate

	public void RefreshResourceCache(WindowTheme windowTheme)
	{
		LayerHost.WindowTheme = windowTheme;
	}

	public RectD PaddedRect { get; private set; }

	protected override void OnInitialized()
	{
		base.OnInitialized();

		ApplySize();
		InvalidateVisual();
	}

	public MapControl()
	{
		LayerHost.RefreshRequested += () => Dispatcher.UIThread.Post(InvalidateVisual);
	}

	#region Control
	private readonly Dictionary<IPointer, ScreenPosition> _positions = [];
	private readonly ManipulationTracker _manipulationTracker = new();
	private readonly FlingTracker _flingTracker = new();
	private readonly TapGestureTracker _tapGestureTracker = new();
	private bool _wasMultiTouch;

	protected override void OnPointerPressed(PointerPressedEventArgs e)
	{
		var pos = e.GetCurrentPoint(this).Position;
		_positions[e.Pointer] = new ScreenPosition(pos.X, pos.Y);

		// 最初のタッチの場合
		if (_positions.Count == 1)
		{
			_wasMultiTouch = false;
			_flingTracker.Restart();
			_tapGestureTracker.Restart(new ScreenPosition(pos.X, pos.Y));
		}
		else
		{
			_wasMultiTouch = true;
		}

		// ManipulationTrackerをリスタート
		_manipulationTracker.Restart(GetPositions());

		base.OnPointerPressed(e);
	}

	protected override void OnPointerMoved(PointerEventArgs e)
	{
		if (!_positions.ContainsKey(e.Pointer))
			return;

		var pos = e.GetCurrentPoint(this).Position;
		_positions[e.Pointer] = new ScreenPosition(pos.X, pos.Y);

		// フリングトラッカーにイベントを追加（1点タッチの場合のみ）
		if (_positions.Count == 1)
			_flingTracker.AddEvent(new ScreenPosition(pos.X, pos.Y), DateTime.Now.Ticks);

		if (IsDisableManualControl || IsNavigating)
			return;

		// ManipulationTrackerでパン・ズームを処理
		_manipulationTracker.Manipulate(GetPositions(), OnManipulation);

		base.OnPointerMoved(e);
	}

	/// <summary>
	/// ManipulationTrackerからの操作を適用
	/// </summary>
	private void OnManipulation(Manipulation manipulation)
	{
		var centerScreenPos = GetScreenCenter();

		// スケール変更（ズーム）
		if (Math.Abs(manipulation.ScaleFactor - 1.0) > 0.001)
		{
			var newZoom = Math.Clamp(Zoom + Math.Log(manipulation.ScaleFactor, 2), MinZoom, MaxZoom);
			var zoomCenter = manipulation.Center;
			var zoomCenterLoc = GetLocation(new Point(zoomCenter.X, zoomCenter.Y));

			var newCenterPix = CenterLocation.ToPixel(newZoom);
			var goalCenterPix = zoomCenterLoc.ToPixel(newZoom);

			var paddedRect = PaddedRect;
			var newZoomCenterPix = new PointD(
				newCenterPix.X + ((paddedRect.Width / 2) - zoomCenter.X) + paddedRect.Left,
				newCenterPix.Y + ((paddedRect.Height / 2) - zoomCenter.Y) + paddedRect.Top);

			Zoom = newZoom;
			CenterLocation = (newCenterPix - (goalCenterPix - newZoomCenterPix)).ToLocation(newZoom);
		}

		// パン（移動）
		var deltaX = manipulation.Center.X - manipulation.PreviousCenter.X;
		var deltaY = manipulation.Center.Y - manipulation.PreviousCenter.Y;
		if (Math.Abs(deltaX) > 0.001 || Math.Abs(deltaY) > 0.001)
		{
			CenterLocation = (CenterLocation.ToPixel(Zoom) - new PointD(deltaX, deltaY)).ToLocation(Zoom);
		}
	}

	private ScreenPosition GetScreenCenter()
		=> new(PaddedRect.Left + PaddedRect.Width / 2, PaddedRect.Top + PaddedRect.Height / 2);

	private ReadOnlySpan<ScreenPosition> GetPositions()
	{
		var positions = new ScreenPosition[_positions.Count];
		var i = 0;
		foreach (var pos in _positions.Values)
			positions[i++] = pos;
		return positions;
	}

	private Location GetLocation(Point p)
	{
		var centerPix = CenterLocation.ToPixel(Zoom);
		var originPix = new PointD(centerPix.X + ((PaddedRect.Width / 2) - p.X) + PaddedRect.Left, centerPix.Y + ((PaddedRect.Height / 2) - p.Y) + PaddedRect.Top);
		return originPix.ToLocation(Zoom);
	}

	protected override void OnPointerReleased(PointerReleasedEventArgs e)
	{
		var endPos = e.GetCurrentPoint(this).Position;

		_positions.Remove(e.Pointer);

		// ManipulationTrackerを更新
		_manipulationTracker.Restart(GetPositions());

		// 全ての指が離れた場合のみジェスチャー処理を実行
		if (!IsDisableManualControl && !IsNavigating && !_wasMultiTouch && _positions.Count == 0)
		{
			var handled = _tapGestureTracker.TapIfNeeded(
				new ScreenPosition(endPos.X, endPos.Y),
				8.0,
				(pos, gestureType) => OnGesture(pos, GetLocation(new Point(pos.X, pos.Y)), gestureType, e.InitialPressMouseButton));

			if (!handled)
			{
				_flingTracker.FlingIfNeeded((vx, vy) => StartFlingAnimation(vx, vy));
			}
		}

		base.OnPointerReleased(e);
	}

	/// <summary>
	/// ジェスチャーイベントを処理
	/// </summary>
	private bool OnGesture(ScreenPosition screenPosition, Location location, GestureType gestureType, MouseButton button)
	{
		switch (gestureType)
		{
			case GestureType.SingleTap:
				// レイヤーにクリックイベントを伝播
				LayerHost.OnMouseClick(location, new PointD(screenPosition.X, screenPosition.Y), button, RenderParameter);
				return true;

			case GestureType.DoubleTap:
				// ダブルタップでズームイン（タップ位置を中心に）
				if (!IsDisableManualControl)
				{
					var mousePos = new Point(screenPosition.X, screenPosition.Y);
					var newZoom = Math.Min(Zoom + 1, MaxZoom);
					if (Math.Abs(newZoom - Zoom) > 0.001)
					{
						var newCenterPix = CenterLocation.ToPixel(newZoom);
						var goalMousePix = location.ToPixel(newZoom);

						var paddedRect = PaddedRect;
						var newMousePix = new PointD(newCenterPix.X + ((paddedRect.Width / 2) - mousePos.X) + paddedRect.Left, newCenterPix.Y + ((paddedRect.Height / 2) - mousePos.Y) + paddedRect.Top);

						Zoom = newZoom;
						CenterLocation = (newCenterPix - (goalMousePix - newMousePix)).ToLocation(newZoom);
					}
					return true;
				}
				return false;

			case GestureType.LongPress:
				// 将来の拡張用（コンテキストメニュー等）
				return false;

			default:
				return false;
		}
	}

	/// <summary>
	/// フリングアニメーションを開始
	/// </summary>
	private void StartFlingAnimation(double velocityX, double velocityY)
	{
		// 速度から移動距離を計算（減衰係数を適用）
		const double decayFactor = 0.15;
		var deltaX = velocityX * decayFactor;
		var deltaY = velocityY * decayFactor;

		var currentCenter = CenterLocation.ToPixel(Zoom);
		var targetCenter = new PointD(currentCenter.X - deltaX, currentCenter.Y - deltaY);
		var targetLocation = targetCenter.ToLocation(Zoom);

		// 簡易的なアニメーション（Navigate機能を使用）
		var halfRenderSize = new PointD(PaddedRect.Width / 2, PaddedRect.Height / 2);
		var leftTop = targetCenter - halfRenderSize;
		var rightBottom = targetCenter + halfRenderSize;

		Navigate(new NavigateAnimation(
			Zoom,
			MinZoom,
			MaxZoom,
			new RectD(CenterLocation.ToPixel(Zoom) - halfRenderSize, CenterLocation.ToPixel(Zoom) + halfRenderSize),
			new RectD(leftTop, rightBottom),
			TimeSpan.FromMilliseconds(300),
			PaddedRect));
	}
	protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
	{
		if (IsDisableManualControl || IsNavigating)
			return;

		var mousePos = e.GetCurrentPoint(this).Position;
		var mouseLoc = GetLocation(mousePos);

		var newZoom = Math.Clamp(Zoom + e.Delta.Y * 0.25, MinZoom, MaxZoom);
		if (Math.Abs(newZoom - Zoom) < .001)
			return;

		var newCenterPix = CenterLocation.ToPixel(newZoom);
		var goalMousePix = mouseLoc.ToPixel(newZoom);

		var paddedRect = PaddedRect;
		var newMousePix = new PointD(newCenterPix.X + ((paddedRect.Width / 2) - mousePos.X) + paddedRect.Left, newCenterPix.Y + ((paddedRect.Height / 2) - mousePos.Y) + paddedRect.Top);

		Zoom = newZoom;
		CenterLocation = (newCenterPix - (goalMousePix - newMousePix)).ToLocation(newZoom);
		base.OnPointerWheelChanged(e);
	}
	#endregion Control

	public override void Render(DrawingContext context)
	{
		if (NavigateAnimation != null)
		{
			(Zoom, CenterLocation) = NavigateAnimation.GetCurrentParameter(Zoom, PaddedRect);
			if (!IsNavigating)
				NavigateAnimation = null;
		}

		if (Layers is null || !IsVisible)
			return;

		context.Custom(this);
	}
	public bool HitTest(Point p) => true;
	public void Render(ImmediateDrawingContext context)
	{
		if (!context.TryGetFeature<ISkiaSharpApiLeaseFeature>(out var leaseFeature))
			return;
		using var lease = leaseFeature.Lease();
		var canvas = lease.SkCanvas;

		var needUpdate = false;
		var param = RenderParameter;
		var shouldRecordMetrics = IsMetricsEnabled && DateTime.Now - _lastMetricsRecordTime >= MetricsRecordInterval;

		var frameStopwatch = shouldRecordMetrics ? Stopwatch.StartNew() : null;

		canvas.Save();
		try
		{
			lock (LayerHost)
			{
				if (shouldRecordMetrics)
				{
					needUpdate = LayerHost.RenderWithMetrics(canvas, param, IsNavigating, out var layerMetrics);
					frameStopwatch!.Stop();

					Dispatcher.UIThread.Post(() =>
						LatestMetrics = new FrameRenderMetrics
						{
							TotalFrameTime = frameStopwatch.Elapsed,
							LayerMetrics = layerMetrics,
							Timestamp = DateTime.Now,
							IsNavigating = IsNavigating,
							Zoom = param.Zoom,
							LeftTopLocation = param.LeftTopLocation,
							LeftTopPixel = param.LeftTopPixel,
							PixelBound = param.PixelBound,
							ViewAreaRect = param.ViewAreaRect
						}
					);

					_lastMetricsRecordTime = DateTime.Now;
				}
				else
				{
					needUpdate = LayerHost.Render(canvas, param, IsNavigating);
				}
			}
		}
		finally
		{
			canvas.Restore();
		}

		if ((!IsHeadlessMode && needUpdate) || (NavigateAnimation?.IsRunning ?? false))
			Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Background);
	}
	public void Dispose() => GC.SuppressFinalize(this);
	public bool Equals(ICustomDrawOperation? other) => false;

	private LayerRenderParameter RenderParameter { get; set; }

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);

		if (change.Property.Name == nameof(Bounds))
			ApplySize();
	}

	private void ApplySize()
	{
		// DP Cache
		var renderSize = Bounds;
		PaddedRect = new RectD(new PointD(Padding.Left, Padding.Top), new PointD(Math.Max(0, renderSize.Width - Padding.Right), Math.Max(0, renderSize.Height - Padding.Bottom)));

		var halfRenderSize = new PointD(PaddedRect.Width / 2, PaddedRect.Height / 2);
		// 左上/右下のピクセル座標
		var leftTop = CenterLocation.ToPixel(Zoom) - halfRenderSize - new PointD(Padding.Left, Padding.Top);
		var rightBottom = CenterLocation.ToPixel(Zoom) + halfRenderSize + new PointD(Padding.Right, Padding.Bottom);

		var leftTopLocation = leftTop.ToLocation(Zoom).CastPoint();

		RenderParameter = new()
		{
			LeftTopLocation = leftTopLocation,
			LeftTopPixel = leftTop,
			PixelBound = new RectD(leftTop, rightBottom),
			ViewAreaRect = new RectD(leftTopLocation, rightBottom.ToLocation(Zoom).CastPoint()),
			Padding = Padding,
			Zoom = Zoom,
		};
	}
}
