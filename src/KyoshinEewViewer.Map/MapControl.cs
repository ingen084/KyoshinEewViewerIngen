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

	public void Navigate(Rect bound, TimeSpan duration)
		=> Navigate(new RectD(bound.X, bound.Y, bound.Width, bound.Height), duration);

	// 指定した範囲をすべて表示できるように調整する
	public void Navigate(RectD bound, TimeSpan duration)
	{
		var boundPixel = new RectD(bound.TopLeft.CastLocation().ToPixel(Zoom), bound.BottomRight.CastLocation().ToPixel(Zoom));
		var centerPixel = CenterLocation.ToPixel(Zoom);
		var halfRect = new PointD(PaddedRect.Width / 2, PaddedRect.Height / 2);
		var leftTop = centerPixel - halfRect;
		var rightBottom = centerPixel + halfRect;
		Navigate(new NavigateAnimation(
				Zoom,
				MinZoom,
				MaxNavigateZoom,
				new RectD(leftTop, rightBottom),
				boundPixel,
				duration,
				PaddedRect));
	}
	internal void Navigate(NavigateAnimation parameter)
	{
		NavigateAnimation = parameter;
		NavigateAnimation.Start();
		Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
	}

	public RectD PaddedRect { get; private set; }

	//private List<MapLayer> Layers { get; } = new();
	//private ImageTileLayer? ImageTileLayer { get; set; }
	//private LandLayer? LandLayer { get; set; }
	// private LandBorderLayer? LandBorderLayer { get; set; }
	//private OverlayLayer? OverlayLayer { get; set; }
	//private RealtimeOverlayLayer? RealtimeOverlayLayer { get; set; }

	protected override void OnInitialized()
	{
		base.OnInitialized();

		//KyoshinEewViewer.Map.Layers.Add(LandLayer = new LandLayer(Projection));
		//LandLayer.RefreshResourceCache(this);
		//if (Map is not null)
		//	LandLayer.Map = Map;
		//else
		//	Task.Run(async () =>
		//	{
		//		Map = new();
		//		await Map.LoadAsync(TopologyMap.LoadCollection(Properties.Resources.DefaultMap));
		//		await Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
		//	});

		//KyoshinEewViewer.Map.Layers.Add(ImageTileLayer = new ImageTileLayer(Projection)
		//{
		//	ImageTileProviders = ImageTileProviders,
		//});
		//KyoshinEewViewer.Map.Layers.Add(/*LandBorderLayer = */new LandBorderLayer(LandLayer, Projection));
		//KyoshinEewViewer.Map.Layers.Add(OverlayLayer = new OverlayLayer(Projection)
		//{
		//	RenderObjects = RenderObjects,
		//});
		//KyoshinEewViewer.Map.Layers.Add(RealtimeOverlayLayer = new RealtimeOverlayLayer(Projection)
		//{
		//	RealtimeRenderObjects = RealtimeRenderObjects,
		//	StandByRenderObjects = StandByRealtimeRenderObjects,
		//});
		//if (IsShowGrid)
		//	KyoshinEewViewer.Map.Layers.Add(new GridLayer(Projection));
		ApplySize();
		InvalidateVisual();
	}

	public bool HitTest(Point p) => true;
	public bool Equals(ICustomDrawOperation? other) => false;

	public void Render(IDrawingContextImpl context)
	{
		var canvas = (context as ISkiaDrawingContextImpl)?.SkCanvas;
		if (canvas == null)
		{
			context.Clear(Colors.Magenta);
			return;
		}
		canvas.Save();

		if (Layers != null)
			foreach (var layer in Layers.ToArray())
				layer.Render(canvas, IsNavigating);
		//LandLayer?.Render(canvas, IsNavigating);
		//OverlayLayer?.Render(canvas, IsNavigating);
		//RealtimeOverlayLayer?.Render(canvas, IsNavigating);
		//LandLayer?.RenderLines(canvas);

		canvas.Restore();
	}

	public override void Render(DrawingContext context)
	{
		if (NavigateAnimation != null)
		{
			var (zoom, loc) = NavigateAnimation.GetCurrentParameter(Zoom, PaddedRect);
			Zoom = zoom;
			CenterLocation = loc;
			if (!IsNavigating)
				NavigateAnimation = null;
		}
		context.Custom(this);

		// NOTE: ここの探索地味に負荷になりそう？
		if ((Layers?.Any(l => l.NeedPersistentUpdate) ?? false) || (NavigateAnimation?.IsRunning ?? false))
			Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
	}

	protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
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
		var renderSize = Bounds; //RenderSize;
		PaddedRect = new RectD(new PointD(Padding.Left, Padding.Top), new PointD(Math.Max(0, renderSize.Width - Padding.Right), Math.Max(0, renderSize.Height - Padding.Bottom)));

		var halfRenderSize = new PointD(PaddedRect.Width / 2, PaddedRect.Height / 2);
		// 左上/右下のピクセル座標
		var leftTop = CenterLocation.ToPixel(Zoom) - halfRenderSize - new PointD(Padding.Left, Padding.Top);
		var rightBottom = CenterLocation.ToPixel(Zoom) + halfRenderSize + new PointD(Padding.Right, Padding.Bottom);

		var leftTopLocation = leftTop.ToLocation(Zoom).CastPoint();
		var viewAreaRect = new RectD(leftTopLocation, rightBottom.ToLocation(Zoom).CastPoint());
		var pixelBound = new RectD(leftTop, rightBottom);

		foreach (var layer in Layers)
		{
			layer.LeftTopLocation = leftTopLocation;
			layer.LeftTopPixel = leftTop;
			layer.PixelBound = pixelBound;
			layer.ViewAreaRect = viewAreaRect;
			layer.Zoom = Zoom;
		}
	}

	public void Dispose() => GC.SuppressFinalize(this);
}
