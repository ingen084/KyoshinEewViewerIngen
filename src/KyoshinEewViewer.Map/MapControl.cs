using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using KyoshinEewViewer.Map.Layers;
using KyoshinMonitorLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Map;

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

	private MapLayer[]? _layers = null;
	public static readonly DirectProperty<MapControl, MapLayer[]?> LayersProperty =
		AvaloniaProperty.RegisterDirect<MapControl, MapLayer[]?>(
			nameof(Layers),
			o => o.Layers,
			(o, v) => o.Layers = v,
			null
		);
	public MapLayer[]? Layers
	{
		get => _layers;
		set {
			if (_layers == value)
				return;

			// デタッチ
			if (_layers != null)
				foreach (var layer in _layers)
					layer.Detach(this);

			// アタッチ
			if (value != null)
				foreach (var layer in value)
				{
					layer.Attach(this);
					layer.RefreshResourceCache(this);
				}

			_layers = value;
			Dispatcher.UIThread.Post(() =>
			{
				ApplySize();
				InvalidateVisual();
			});
		}
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

	public void RefreshResourceCache()
	{
		if (Layers == null)
			return;
		foreach (var layer in Layers.ToArray())
			layer.RefreshResourceCache(this);
		InvalidateVisual();
	}

	public RectD PaddedRect { get; private set; }

	protected override void OnInitialized()
	{
		base.OnInitialized();

		ApplySize();
		InvalidateVisual();
	}

	#region Control
	private Dictionary<IPointer, Point> StartPoints { get; } = [];
	protected override void OnPointerPressed(PointerPressedEventArgs e)
	{
		var originPos = e.GetCurrentPoint(this).Position;
		StartPoints[e.Pointer] = originPos;
		// 3点以上の場合は2点になるようにする
		if (StartPoints.Count > 2)
			foreach (var pointer in StartPoints.Where(p => p.Key != e.Pointer).Select(p => p.Key).ToArray())
			{
				if (StartPoints.Count <= 2)
					break;
				StartPoints.Remove(pointer);
			}
		base.OnPointerPressed(e);

	}
	protected override void OnPointerMoved(PointerEventArgs e)
	{
		if (!StartPoints.TryGetValue(e.Pointer, out var beforePoint))
			return;
		var newPosition = e.GetCurrentPoint(this).Position;
		var vector = beforePoint - newPosition;
		if (vector == Vector.Zero)
			return;
		StartPoints[e.Pointer] = newPosition;

		if (IsDisableManualControl || IsNavigating)
			return;

		if (StartPoints.Count <= 1)
			CenterLocation = (CenterLocation.ToPixel(Zoom) + (PointD)vector).ToLocation(Zoom);
		else
		{
			var lockPos = StartPoints.First(p => p.Key != e.Pointer).Value;

			var befLen = GetLength(lockPos - beforePoint);
			var newLen = GetLength(lockPos - newPosition);
			var lockLoc = GetLocation(lockPos);

			var df = (befLen > newLen ? -1 : 1) * GetLength(vector) * .005;
			if (Math.Abs(df) < .01)
			{
				CenterLocation = (CenterLocation.ToPixel(Zoom) + (PointD)vector).ToLocation(Zoom);
				return;
			}
			Zoom += df;

			var newCenterPix = CenterLocation.ToPixel(Zoom);
			var goalOriginPix = lockLoc.ToPixel(Zoom);

			var paddedRect = PaddedRect;
			var newMousePix = new PointD(newCenterPix.X + ((paddedRect.Width / 2) - lockPos.X) + paddedRect.Left, newCenterPix.Y + ((paddedRect.Height / 2) - lockPos.Y) + paddedRect.Top);
			CenterLocation = (newCenterPix - (goalOriginPix - newMousePix)).ToLocation(Zoom);
		}
		base.OnPointerMoved(e);
	}
	private static double GetLength(Point p)
		=> Math.Sqrt(p.X * p.X + p.Y * p.Y);
	private Location GetLocation(Point p)
	{
		var centerPix = CenterLocation.ToPixel(Zoom);
		var originPix = new PointD(centerPix.X + ((PaddedRect.Width / 2) - p.X) + PaddedRect.Left, centerPix.Y + ((PaddedRect.Height / 2) - p.Y) + PaddedRect.Top);
		return originPix.ToLocation(Zoom);
	}
	protected override void OnPointerReleased(PointerReleasedEventArgs e)
	{
		StartPoints.Remove(e.Pointer);
		base.OnPointerReleased(e);
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
		if (Layers is null)
			return;

		var needUpdate = false;
		var param = RenderParameter;

		canvas.Save();
		try
		{
			lock (Layers)
				foreach (var layer in Layers)
				{
					layer.Render(canvas, param, IsNavigating);
					if (!needUpdate && layer.NeedPersistentUpdate)
						needUpdate = true;
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
		if (Layers == null)
			return;

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
