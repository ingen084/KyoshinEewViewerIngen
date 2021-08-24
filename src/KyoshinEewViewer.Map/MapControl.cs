using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using KyoshinEewViewer.Map.Layers;
using KyoshinEewViewer.Map.Layers.ImageTile;
using KyoshinEewViewer.Map.Projections;
using KyoshinMonitorLib;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Map
{
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
				(o, v) =>
				{
					o.zoom = Math.Min(Math.Max(v, o.MinZoom), o.MaxZoom);
					if (o.zoom == v)
						return;
					Dispatcher.UIThread.InvokeAsync(() =>
					{
						o.ApplySize();
						o.InvalidateVisual();
					}, DispatcherPriority.Background).ConfigureAwait(false);
				});
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

		private double maxZoom = 12;
		public static readonly DirectProperty<MapControl, double> MaxZoomProperty =
			AvaloniaProperty.RegisterDirect<MapControl, double>(
				nameof(MaxZoom),
				o => o.MaxZoom,
				(o, v) =>
				{
					o.maxZoom = v;
					o.Zoom = o.Zoom;
				});
		public double MaxZoom
		{
			get => maxZoom;
			set {
				maxZoom = value;
				Zoom = zoom;
			}
		}

		private double maxNavigateZoom = 10;
		public static readonly DirectProperty<MapControl, double> MaxNavigateZoomProperty =
			AvaloniaProperty.RegisterDirect<MapControl, double>(
				nameof(MaxNavigateZoom),
				o => o.MaxNavigateZoom,
				(o, v) => o.maxNavigateZoom = v);
		public double MaxNavigateZoom
		{
			get => maxNavigateZoom;
			set => maxNavigateZoom = value;
		}

		private double minZoom = 4;
		public static readonly DirectProperty<MapControl, double> MinZoomProperty =
			AvaloniaProperty.RegisterDirect<MapControl, double>(
				nameof(MinZoom),
				o => o.MinZoom,
				(o, v) =>
				{
					o.minZoom = v;
					o.Zoom = o.Zoom;
				});
		public double MinZoom
		{
			get => minZoom;
			set => minZoom = value;
		}

		private Dictionary<LandLayerType, TopologyMap> map = new();
		public Dictionary<LandLayerType, TopologyMap> Map
		{
			get => map;
			set {
				if (map == value)
					return;
				map = value;

				Task.Run(async () =>
				{
					if (LandLayer != null)
						LandLayer.SetupMap(map);
					await Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
				}).ConfigureAwait(false);
			}
		}

		private Dictionary<LandLayerType, Dictionary<int, SKColor>>? customColorMap = null;
		public static readonly DirectProperty<MapControl, Dictionary<LandLayerType, Dictionary<int, SKColor>>?> CustomColorMapProperty =
			AvaloniaProperty.RegisterDirect<MapControl, Dictionary<LandLayerType, Dictionary<int, SKColor>>?>(
				nameof(CustomColorMap),
				o => o.CustomColorMap,
				(o, v) =>
				{
					o.customColorMap = v;

					Task.Run(async () =>
					{
						if (o.LandLayer != null)
							o.LandLayer.CustomColorMap = o.customColorMap;
						await Dispatcher.UIThread.InvokeAsync(o.InvalidateVisual, DispatcherPriority.Background);
					}).ConfigureAwait(false);
				});
		public Dictionary<LandLayerType, Dictionary<int, SKColor>>? CustomColorMap
		{
			get => customColorMap;
			set {
				if (customColorMap == value)
					return;
				customColorMap = value;

				Task.Run(async () =>
				{
					if (LandLayer != null)
						LandLayer.CustomColorMap = customColorMap;
					await Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
				}).ConfigureAwait(false);
			}
		}

		private ImageTileProvider[]? imageTileProviders;
		public static readonly DirectProperty<MapControl, ImageTileProvider[]?> ImageTileProvidersProperty =
			AvaloniaProperty.RegisterDirect<MapControl, ImageTileProvider[]?>(
				nameof(ImageTileProviders),
				o => o.ImageTileProviders,
				(o, v) => o.ImageTileProviders = v);
		public ImageTileProvider[]? ImageTileProviders
		{
			get => imageTileProviders;
			set {
				if (imageTileProviders != null)
					foreach (var p in imageTileProviders)
						p.ImageFetched -= ImageUpdatedHandler;

				imageTileProviders = value;

				if (imageTileProviders != null)
					foreach (var p in imageTileProviders)
						p.ImageFetched += ImageUpdatedHandler;

				if (ImageTileLayer != null)
					ImageTileLayer.ImageTileProviders = imageTileProviders;
				Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background).ConfigureAwait(false);
			}
		}
		private void ImageUpdatedHandler()
			=> Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background).ConfigureAwait(false);

		private IRenderObject[]? renderObjects;
		public static readonly DirectProperty<MapControl, IRenderObject[]?> RenderObjectsProperty =
			AvaloniaProperty.RegisterDirect<MapControl, IRenderObject[]?>(
				nameof(RenderObjects),
				o => o.RenderObjects,
				(o, v) =>
				{
					o.renderObjects = v;
					if (o.OverlayLayer != null)
						o.OverlayLayer.RenderObjects = o.renderObjects;
					Dispatcher.UIThread.InvokeAsync(o.InvalidateVisual, DispatcherPriority.Background).ConfigureAwait(false);
				});
		public IRenderObject[]? RenderObjects
		{
			get => renderObjects;
			set {
				SetAndRaise(RenderObjectsProperty, ref renderObjects, value);
				if (OverlayLayer != null)
					OverlayLayer.RenderObjects = renderObjects;
				Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background).ConfigureAwait(false);
			}
		}

		private RealtimeRenderObject[]? realtimeRenderObjects;
		public static readonly DirectProperty<MapControl, RealtimeRenderObject[]?> RealtimeRenderObjectsProperty =
			AvaloniaProperty.RegisterDirect<MapControl, RealtimeRenderObject[]?>(
				nameof(RealtimeRenderObjects),
				o => o.RealtimeRenderObjects,
				(o, v) =>
				{
					o.realtimeRenderObjects = v;
					if (o.RealtimeOverlayLayer != null)
						o.RealtimeOverlayLayer.RealtimeRenderObjects = o.realtimeRenderObjects;
					Dispatcher.UIThread.InvokeAsync(o.InvalidateVisual, DispatcherPriority.Background).ConfigureAwait(false);
				});
		public RealtimeRenderObject[]? RealtimeRenderObjects
		{
			get => realtimeRenderObjects;
			set => SetAndRaise(RealtimeRenderObjectsProperty, ref realtimeRenderObjects, value);
		}

		private RealtimeRenderObject[]? standByRealtimeRenderObjects;
		public static readonly DirectProperty<MapControl, RealtimeRenderObject[]?> StandByRealtimeRenderObjectsProperty =
			AvaloniaProperty.RegisterDirect<MapControl, RealtimeRenderObject[]?>(
				nameof(StandByRealtimeRenderObjects),
				o => o.StandByRealtimeRenderObjects,
				(o, v) =>
				{
					o.standByRealtimeRenderObjects = v;
					if (o.RealtimeOverlayLayer != null)
						o.RealtimeOverlayLayer.StandByRenderObjects = o.StandByRealtimeRenderObjects;
					Dispatcher.UIThread.InvokeAsync(o.InvalidateVisual, DispatcherPriority.Background).ConfigureAwait(false);
				});
		public RealtimeRenderObject[]? StandByRealtimeRenderObjects
		{
			get => standByRealtimeRenderObjects;
			set => SetAndRaise(StandByRealtimeRenderObjectsProperty, ref standByRealtimeRenderObjects, value);
		}

		private Thickness padding = new();
		public static readonly DirectProperty<MapControl, Thickness> PaddingProperty =
			AvaloniaProperty.RegisterDirect<MapControl, Thickness>(
				nameof(Padding),
				o => o.padding,
				(o, v) =>
				{
					o.padding = v;
					Dispatcher.UIThread.InvokeAsync(() =>
					{
						o.ApplySize();
						o.InvalidateVisual();
					}, DispatcherPriority.Background).ConfigureAwait(false);
				});

		public Thickness Padding
		{
			get => padding;
			set {
				SetAndRaise(PaddingProperty, ref padding, value);

				Dispatcher.UIThread.InvokeAsync(() =>
				{
					ApplySize();
					InvalidateVisual();
				}, DispatcherPriority.Background).ConfigureAwait(false);
			}
		}

		public static readonly StyledProperty<bool> IsShowGridProperty =
			AvaloniaProperty.Register<MapControl, bool>(nameof(IsShowGrid), coerce: (s, v) =>
			{
				if (s is not MapControl map)
					return v;

				GridLayer? layer;
				layer = (GridLayer?)map.Layers.FirstOrDefault(l => l is GridLayer);
				if (v)
				{
					if (layer == null)
						map.Layers.Add(new GridLayer(map.Projection));
				}
				else if (layer != null)
					map.Layers.Remove(layer);

				Dispatcher.UIThread.InvokeAsync(() =>
				{
					map.ApplySize();
					map.InvalidateVisual();
				}, DispatcherPriority.Background).ConfigureAwait(false);
				return v;
			});

		public bool IsShowGrid
		{
			get => GetValue(IsShowGridProperty);
			set => SetValue(IsShowGridProperty, value);
		}

		public MapProjection Projection { get; set; } = new MillerProjection();

		private NavigateAnimation? NavigateAnimation { get; set; }
		public bool IsNavigating => NavigateAnimation?.IsRunning ?? false;

		public void RefreshResourceCache()
		{
			LandLayer?.RefreshResourceCache(this);
			OverlayLayer?.RefreshResourceCache(this);
			InvalidateVisual();
		}

		public void Navigate(Rect bound, TimeSpan duration)
			=> Navigate(new RectD(bound.X, bound.Y, bound.Width, bound.Height), duration);

		// 指定した範囲をすべて表示できるように調整する
		public void Navigate(RectD bound, TimeSpan duration)
		{
			var boundPixel = new RectD(bound.TopLeft.CastLocation().ToPixel(Projection, Zoom), bound.BottomRight.CastLocation().ToPixel(Projection, Zoom));
			var centerPixel = CenterLocation.ToPixel(Projection, Zoom);
			var halfRect = new PointD(PaddedRect.Width / 2, PaddedRect.Height / 2);
			var leftTop = centerPixel - halfRect;
			var rightBottom = centerPixel + halfRect;
			Navigate(new NavigateAnimation(
					Zoom,
					maxNavigateZoom,
					new RectD(leftTop, rightBottom),
					boundPixel,
					duration,
					PaddedRect,
					Projection));
		}
		internal void Navigate(NavigateAnimation parameter)
		{
			NavigateAnimation = parameter;
			NavigateAnimation.Start();
			Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
		}


		public RectD PaddedRect { get; private set; }

		private List<MapLayerBase> Layers { get; } = new();
		private ImageTileLayer? ImageTileLayer { get; set; }
		private LandLayer? LandLayer { get; set; }
		private OverlayLayer? OverlayLayer { get; set; }
		private RealtimeOverlayLayer? RealtimeOverlayLayer { get; set; }

		protected override void OnInitialized()
		{
			base.OnInitialized();

			Layers.Add(LandLayer = new LandLayer(Projection));
			LandLayer.RefreshResourceCache(this);
			if (Map.Any())
				Task.Run(async () =>
				{
					LandLayer.SetupMap(Map);
					await Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background).ConfigureAwait(false);
				}).ConfigureAwait(false);
			else
				Map = TopologyMap.LoadCollection(Properties.Resources.DefaultMap);

			Layers.Add(ImageTileLayer = new ImageTileLayer(Projection) 
			{
				ImageTileProviders = ImageTileProviders,
			});
			Layers.Add(OverlayLayer = new OverlayLayer(Projection)
			{
				RenderObjects = RenderObjects,
			});
			Layers.Add(RealtimeOverlayLayer = new RealtimeOverlayLayer(Projection)
			{
				RealtimeRenderObjects = RealtimeRenderObjects,
				StandByRenderObjects = StandByRealtimeRenderObjects,
			});
			if (IsShowGrid)
				Layers.Add(new GridLayer(Projection));
			ApplySize();
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

			foreach (var layer in Layers)
				layer.Render(canvas, IsNavigating);
			//LandLayer?.Render(canvas, IsNavigating);
			//OverlayLayer?.Render(canvas, IsNavigating);
			//RealtimeOverlayLayer?.Render(canvas, IsNavigating);
			LandLayer?.RenderLines(canvas);

			canvas.Restore();
		}

		public override void Render(DrawingContext context)
		{
			if (NavigateAnimation != null)
			{
				var (zoom, loc) = NavigateAnimation.GetCurrentParameter(Projection, Zoom, PaddedRect);
				Zoom = zoom;
				CenterLocation = loc;
				if (!IsNavigating)
					NavigateAnimation = null;
			}
			context.Custom(this);

			if ((RealtimeRenderObjects?.Any() ?? false) || (NavigateAnimation?.IsRunning ?? false))
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
			// DP Cache
			var renderSize = Bounds; //RenderSize;
			var padding = Padding;
			PaddedRect = new RectD(new PointD(padding.Left, padding.Top), new PointD(Math.Max(0, renderSize.Width - padding.Right), Math.Max(0, renderSize.Height - padding.Bottom)));
			var zoom = Zoom;
			var centerLocation = CenterLocation;

			var halfRenderSize = new PointD(PaddedRect.Width / 2, PaddedRect.Height / 2);
			// 左上/右下のピクセル座標
			var leftTop = centerLocation.ToPixel(Projection, zoom) - halfRenderSize - new PointD(padding.Left, padding.Top);
			var rightBottom = centerLocation.ToPixel(Projection, zoom) + halfRenderSize + new PointD(padding.Right, padding.Bottom);

			var leftTopLocation = leftTop.ToLocation(Projection, zoom).CastPoint();
			var viewAreaRect = new RectD(leftTopLocation, rightBottom.ToLocation(Projection, zoom).CastPoint());
			var pixelBound = new RectD(leftTop, rightBottom);

			foreach (var layer in Layers)
			{
				layer.LeftTopLocation = leftTopLocation;
				layer.LeftTopPixel = leftTop;
				layer.PixelBound = pixelBound;
				layer.ViewAreaRect = viewAreaRect;
				layer.Zoom = zoom;
			}
		}

		public void Dispose() => GC.SuppressFinalize(this);
	}
}
