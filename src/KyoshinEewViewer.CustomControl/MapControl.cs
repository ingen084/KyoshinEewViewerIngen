using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering;
using Avalonia.Rendering.Composition;
using Avalonia.Skia;
using Avalonia.Threading;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Metrics;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Layers;
using KyoshinMonitorLib;
using SkiaSharp;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using Location = KyoshinMonitorLib.Location;

namespace KyoshinEewViewer.CustomControl;

public partial class MapControl : Avalonia.Controls.Control, ICustomHitTest
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
				RequestRedraw();
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
				RequestRedraw();
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
				RequestRedraw();
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

	public static readonly DirectProperty<MapControl, bool> IsInertiaEnabledProperty =
		AvaloniaProperty.RegisterDirect<MapControl, bool>(
			nameof(IsInertiaEnabled),
			o => o.IsInertiaEnabled,
			(o, v) => o.IsInertiaEnabled = v
		);
	private bool _isInertiaEnabled = true;
	/// <summary>
	/// 慣性スクロールを有効にするかどうか
	/// </summary>
	public bool IsInertiaEnabled
	{
		get => _isInertiaEnabled;
		set => SetAndRaise(IsInertiaEnabledProperty, ref _isInertiaEnabled, value);
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

		// Navigateは慣性アニメーションより優先
		_inertiaAnimation?.Stop();
		_inertiaAnimation = null;

		if (parameter.Duration <= TimeSpan.Zero)
		{
			(Zoom, CenterLocation) = parameter.GetCurrentParameter(Zoom, PaddedRect);
			Dispatcher.UIThread.Post(() =>
			{
				RequestRedraw();
				RequestAnimationFrameIfNeeded();
			});
			return;
		}
		NavigateAnimation = parameter;
		NavigateAnimation.Start();
		Dispatcher.UIThread.Post(() =>
		{
			RequestRedraw();
			RequestAnimationFrameIfNeeded();
		});
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

	private LayerRenderParameter RenderParameter { get; set; }

	// UI thread が write、render thread が read する snapshot 領域
	private readonly Lock _snapshotLock = new();
	private LayerRenderParameter _renderParamSnapshot;
	private bool _isAnimatingSnapshot;

	// render thread が write、UI thread が read
	private int _needsContinuousFrame;

	private bool _animationFramePending;

	private CompositionCustomVisual? _customVisual;
	private MapVisualHandler? _handler;

	public MapControl()
	{
		LayerHost.RefreshRequested += () => Dispatcher.UIThread.Post(RequestRedraw);
	}

	public bool HitTest(Point p) =>
		p.X >= 0 && p.Y >= 0 && p.X < Bounds.Width && p.Y < Bounds.Height;

	protected override void OnInitialized()
	{
		base.OnInitialized();

		ApplySize();
		RequestRedraw();
	}

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnAttachedToVisualTree(e);

		var compositor = ElementComposition.GetElementVisual(this)?.Compositor;
		if (compositor is null)
			return;

		_handler = new MapVisualHandler(this);
		_customVisual = compositor.CreateCustomVisual(_handler);
		_customVisual.Size = new Vector2((float)Bounds.Width, (float)Bounds.Height);
		ElementComposition.SetElementChildVisual(this, _customVisual);

		ApplySize();
		RequestRedraw();
	}

	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
	{
		// detach 後に進めても意味の無いアニメーションを停止
		NavigateAnimation = null;
		_inertiaAnimation?.Stop();
		_inertiaAnimation = null;
		_wheelZoomTracker.Reset();

		ElementComposition.SetElementChildVisual(this, null);
		_customVisual = null;
		_handler = null;

		base.OnDetachedFromVisualTree(e);
	}

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);

		if (change.Property == BoundsProperty)
		{
			ApplySize();
			if (_customVisual is not null)
			{
				_customVisual.Size = new Vector2((float)Bounds.Width, (float)Bounds.Height);
				RequestRedraw();
			}
		}
	}

	public void RequestRedraw()
	{
		if (!Dispatcher.UIThread.CheckAccess())
		{
			Dispatcher.UIThread.Post(RequestRedraw);
			return;
		}
		lock (_snapshotLock)
		{
			_renderParamSnapshot = RenderParameter;
			_isAnimatingSnapshot = IsNavigating;
		}
		_customVisual?.SendHandlerMessage(MapVisualHandler.RedrawMessage);
	}

	private void RequestAnimationFrameIfNeeded()
	{
		Dispatcher.UIThread.VerifyAccess();
		if (_animationFramePending)
			return;
		if (_customVisual is null)
			return;
		if (TopLevel.GetTopLevel(this) is not { } tl)
			return;
		_animationFramePending = true;
		tl.RequestAnimationFrame(OnAnimationTick);
	}

	private void OnAnimationTick(TimeSpan _)
	{
		_animationFramePending = false;
		// detach 後の遅延コールバックは safely no-op
		if (_customVisual is null)
			return;

		if (NavigateAnimation is not null)
		{
			(Zoom, CenterLocation) = NavigateAnimation.GetCurrentParameter(Zoom, PaddedRect);
			if (!IsNavigating)
				NavigateAnimation = null;
		}

		ProcessInertiaFrame();

		ApplySize();
		RequestRedraw();

		var stillAnimating =
			IsNavigating
			|| (_inertiaAnimation?.IsRunning ?? false)
			|| _wheelZoomTracker.IsRunning
			|| (!IsHeadlessMode && Volatile.Read(ref _needsContinuousFrame) != 0);

		if (stillAnimating)
			RequestAnimationFrameIfNeeded();
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

	private sealed class MapVisualHandler : CompositionCustomVisualHandler
	{
		public static readonly object RedrawMessage = new();

		private readonly MapControl _owner;

		public MapVisualHandler(MapControl owner) => _owner = owner;

		public override void OnMessage(object message)
		{
			if (message == RedrawMessage)
				Invalidate();
		}

		public override void OnRender(ImmediateDrawingContext drawingContext)
		{
			if (!drawingContext.TryGetFeature<ISkiaSharpApiLeaseFeature>(out var leaseFeature))
				return;
			if (_owner.Layers is null)
				return;

			using var lease = leaseFeature.Lease();
			var canvas = lease.SkCanvas;

			LayerRenderParameter param;
			bool isAnim;
			lock (_owner._snapshotLock)
			{
				param = _owner._renderParamSnapshot;
				isAnim = _owner._isAnimatingSnapshot;
			}

			var shouldRecordMetrics = _owner.IsMetricsEnabled && DateTime.Now - _owner._lastMetricsRecordTime >= MetricsRecordInterval;
			var frameStopwatch = shouldRecordMetrics ? Stopwatch.StartNew() : null;

			bool needUpdate;
			canvas.Save();
			try
			{
				lock (_owner.LayerHost)
				{
					if (shouldRecordMetrics)
					{
						needUpdate = _owner.LayerHost.RenderWithMetrics(canvas, param, isAnim, out var layerMetrics);
						frameStopwatch!.Stop();

						var capturedTotal = frameStopwatch.Elapsed;
						var capturedMetrics = layerMetrics;
						var capturedParam = param;
						var capturedIsAnim = isAnim;
						Dispatcher.UIThread.Post(() =>
							_owner.LatestMetrics = new FrameRenderMetrics
							{
								TotalFrameTime = capturedTotal,
								LayerMetrics = capturedMetrics,
								Timestamp = DateTime.Now,
								IsNavigating = capturedIsAnim,
								Zoom = capturedParam.Zoom,
								LeftTopLocation = capturedParam.LeftTopLocation,
								LeftTopPixel = capturedParam.LeftTopPixel,
								PixelBound = capturedParam.PixelBound,
								ViewAreaRect = capturedParam.ViewAreaRect
							}
						);

						_owner._lastMetricsRecordTime = DateTime.Now;
					}
					else
					{
						needUpdate = _owner.LayerHost.Render(canvas, param, isAnim);
					}
				}
			}
			finally
			{
				canvas.Restore();
			}

			Volatile.Write(ref _owner._needsContinuousFrame, needUpdate ? 1 : 0);
			if (needUpdate && !_owner.IsHeadlessMode)
				Dispatcher.UIThread.Post(_owner.RequestAnimationFrameIfNeeded, DispatcherPriority.Background);
		}
	}
}
