using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using KyoshinEewViewer.Map.Layers;
using KyoshinMonitorLib;
using System;
using System.Linq;

namespace KyoshinEewViewer.Map;

public class MapControl : Avalonia.Controls.Control, ICustomDrawOperation
{
	private Location centerLocation = new(0, 0);
	public Location CenterLocation
	{
		get => centerLocation;
		set {
			if (centerLocation == value)
				return;
			centerLocation = value;
			if (centerLocation != null)
			{
				var cl = centerLocation;
				cl.Latitude = Math.Min(Math.Max(cl.Latitude, -80), 80);
				// 1回転させる
				if (cl.Longitude < -180)
					cl.Longitude += 360;
				if (cl.Longitude > 180)
					cl.Longitude -= 360;
				centerLocation = cl;
			}

			Dispatcher.UIThread.InvokeAsync(() =>
			{
				ApplySize();
				InvalidateVisual();
			}, DispatcherPriority.Background).ConfigureAwait(false);
		}
	}

	private double zoom = 4;
	public static readonly DirectProperty<MapControl, double> ZoomProperty =
		AvaloniaProperty.RegisterDirect<MapControl, double>(
			nameof(Zoom),
			o => o.Zoom,
			(o, v) => o.Zoom = v
		);
	public double Zoom
	{
		get => zoom;
		set {
			zoom = Math.Min(Math.Max(value, MinZoom), MaxZoom);
			if (zoom == value)
				return;
			Dispatcher.UIThread.InvokeAsync(() =>
			{
				ApplySize();
				InvalidateVisual();
			}, DispatcherPriority.Background).ConfigureAwait(false);
		}
	}

	private MapLayer[]? layers = null;
	public static readonly DirectProperty<MapControl, MapLayer[]?> LayersProperty =
		AvaloniaProperty.RegisterDirect<MapControl, MapLayer[]?>(
			nameof(Layers),
			o => o.Layers,
			(o, v) => o.Layers = v,
			null
		);
	public MapLayer[]? Layers
	{
		get => layers;
		set {
			if (layers == value)
				return;

			// デタッチ
			if (layers != null)
				foreach (var layer in layers)
					layer.Detach(this);

			// アタッチ
			if (value != null)
				foreach (var layer in value)
				{
					layer.Attach(this);
					layer.RefreshResourceCache(this);
				}

			layers = value;
			Dispatcher.UIThread.InvokeAsync(() =>
			{
				ApplySize();
				InvalidateVisual();
			}, DispatcherPriority.Background).ConfigureAwait(false);
		}
	}

	private double maxZoom = 12;
	public static readonly DirectProperty<MapControl, double> MaxZoomProperty =
		AvaloniaProperty.RegisterDirect<MapControl, double>(
			nameof(MaxZoom),
			o => o.MaxZoom,
			(o, v) => o.MaxZoom = v
		);
	public double MaxZoom
	{
		get => maxZoom;
		set {
			maxZoom = value;
			Zoom = zoom;
		}
	}

	public static readonly DirectProperty<MapControl, double> MaxNavigateZoomProperty =
		AvaloniaProperty.RegisterDirect<MapControl, double>(
			nameof(MaxNavigateZoom),
			o => o.MaxNavigateZoom,
			(o, v) => o.MaxNavigateZoom = v);
	public double MaxNavigateZoom { get; set; } = 10;

	private double minZoom = 4;
	public static readonly DirectProperty<MapControl, double> MinZoomProperty =
		AvaloniaProperty.RegisterDirect<MapControl, double>(
			nameof(MinZoom),
			o => o.MinZoom,
			(o, v) => o.MinZoom = v
		);
	public double MinZoom
	{
		get => minZoom;
		set {
			minZoom = value;
			Zoom = zoom;
		}
	}

	private Thickness padding = new();
	public static readonly DirectProperty<MapControl, Thickness> PaddingProperty =
		AvaloniaProperty.RegisterDirect<MapControl, Thickness>(
			nameof(Padding),
			o => o.Padding,
			(o, v) => o.Padding = v
		);
	public Thickness Padding
	{
		get => padding;
		set {
			padding = value;

			Dispatcher.UIThread.InvokeAsync(() =>
			{
				ApplySize();
				InvalidateVisual();
			}, DispatcherPriority.Background).ConfigureAwait(false);
		}
	}

	public static readonly DirectProperty<MapControl, bool> IsHeadlessModeProperty =
		AvaloniaProperty.RegisterDirect<MapControl, bool>(
			nameof(IsHeadlessMode),
			o => o.IsHeadlessMode,
			(o, v) => o.IsHeadlessMode = v
		);
	public bool IsHeadlessMode { get; set; } = false;

	private NavigateAnimation? NavigateAnimation { get; set; }
	public bool IsNavigating => NavigateAnimation?.IsRunning ?? false;

	public void RefreshResourceCache()
	{
		if (Layers == null)
			return;
		foreach (var layer in Layers.ToArray())
			layer.RefreshResourceCache(this);
		InvalidateVisual();
	}

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
			Dispatcher.UIThread.InvokeAsync(() => Navigate(bound, duration, unlimitNavigateZoom));
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
		if (parameter.Duration <= TimeSpan.Zero && PaddedRect.Width != 0 && PaddedRect.Height != 0)
		{
			(Zoom, CenterLocation) = parameter.GetCurrentParameter(Zoom, PaddedRect);
			Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
			return;
		}
		NavigateAnimation = parameter;
		NavigateAnimation.Start();
		Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
	}

	public RectD PaddedRect { get; private set; }

	protected override void OnInitialized()
	{
		base.OnInitialized();

		ApplySize();
		InvalidateVisual();
	}

	public override void Render(DrawingContext context)
	{
		if (NavigateAnimation != null)
		{
			var (z, loc) = NavigateAnimation.GetCurrentParameter(Zoom, PaddedRect);
			Zoom = z;
			CenterLocation = loc;
			if (!IsNavigating)
				NavigateAnimation = null;
		}

		if (Layers is null)
			return;

		context.Custom(this);
	}
	public bool HitTest(Point p) => true;
	public void Render(IDrawingContextImpl context)
	{
		var leaseFeature = context.GetFeature<ISkiaSharpApiLeaseFeature>();
		if (leaseFeature == null)
			return;
		using var lease = leaseFeature.Lease();
		var canvas = lease.SkCanvas;
		if (Layers is null)
			return;

		canvas.Save();

		var needUpdate = false;
		var param = RenderParameter;

		lock (Layers)
			foreach (var layer in Layers)
			{
				layer.Render(canvas, param, IsNavigating);
				if (!needUpdate && layer.NeedPersistentUpdate)
					needUpdate = true;
			}

		canvas.Restore();

		if (!IsHeadlessMode && (needUpdate || (NavigateAnimation?.IsRunning ?? false)))
			Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
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
