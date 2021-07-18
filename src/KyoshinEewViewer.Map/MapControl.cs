using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using KyoshinEewViewer.Map.Layers;
using KyoshinEewViewer.Map.Projections;
using KyoshinMonitorLib;
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
			set
			{
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

		private double zoom = 5;
		public double Zoom
		{
			get => zoom;
			set
			{
				if (zoom == value)
					return;
				zoom = Math.Min(Math.Max(value, MinZoom), MaxZoom);
				Dispatcher.UIThread.InvokeAsync(() =>
				{
					ApplySize();
					InvalidateVisual();
				}, DispatcherPriority.Background).ConfigureAwait(false);
			}
		}

		private double maxZoom = 12;
		public double MaxZoom
		{
			get => maxZoom;
			set => maxZoom = value;
		}
		private double minZoom = 4;
		public double MinZoom
		{
			get => minZoom;
			set => minZoom = value;
		}

		private Dictionary<LandLayerType, TopologyMap> map = new();
		public Dictionary<LandLayerType, TopologyMap> Map
		{
			get => map;
			set
			{
				if (map == value)
					return;
				map = value;

				Task.Run(async () =>
				{
					if (LandLayer != null)
						await LandLayer.SetupMapAsync(map);
					await Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
				}).ConfigureAwait(false);
			}
		}

		private Dictionary<int, Color> customColorMap = new();
		public Dictionary<int, Color> CustomColorMap
		{
			get => customColorMap;
			set
			{
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

		private IRenderObject[]? renderObjects;
		public static readonly DirectProperty<MapControl, IRenderObject[]?> RenderObjectsProperty =
			AvaloniaProperty.RegisterDirect<MapControl, IRenderObject[]?>(
				nameof(RenderObjects),
				o => o.RenderObjects,
				(o, v) =>
				{
					o.RenderObjects = v;
					if (o.OverlayLayer != null)
						o.OverlayLayer.RenderObjects = o.renderObjects;
					Dispatcher.UIThread.InvokeAsync(o.InvalidateVisual, DispatcherPriority.Background).ConfigureAwait(false);
				});
		public IRenderObject[]? RenderObjects
		{
			get => renderObjects;
			set
			{
				SetAndRaise(RenderObjectsProperty, ref renderObjects, value);
				// MEMO: Avaloniaのバグっぽい
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
					o.RealtimeRenderObjects = v;
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
					o.Padding = v;
					Dispatcher.UIThread.InvokeAsync(() =>
					{
						o.ApplySize();
						o.InvalidateVisual();
					}, DispatcherPriority.Background).ConfigureAwait(false);
				});

		public Thickness Padding
		{
			get => padding;
			set
			{
				SetAndRaise(PaddingProperty, ref padding, value);

				Dispatcher.UIThread.InvokeAsync(() =>
				{
					ApplySize();
					InvalidateVisual();
				}, DispatcherPriority.Background).ConfigureAwait(false);
			}
		}

		public MapProjection Projection { get; set; } = new MillerProjection();

		private NavigateAnimation? NavigateAnimation { get; set; }
		public bool IsNavigating => NavigateAnimation?.IsRunning ?? false;

		public void RefleshResourceCache()
		{
			LandLayer?.RefleshResourceCache(this);
			OverlayLayer?.RefleshResourceCache(this);
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
					new RectD(leftTop, rightBottom),
					boundPixel,
					duration));
		}
		internal void Navigate(NavigateAnimation parameter)
		{
			NavigateAnimation = parameter;
			NavigateAnimation.Start();
			Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
		}


		public RectD PaddedRect { get; private set; }

		private LandLayer? LandLayer { get; set; }
		private OverlayLayer? OverlayLayer { get; set; }
		private RealtimeOverlayLayer? RealtimeOverlayLayer { get; set; }

		protected override void OnInitialized()
		{
			base.OnInitialized();

			LandLayer = new LandLayer(Projection);
			LandLayer.RefleshResourceCache(this);
			if (Map.Any())
				Task.Run(async () =>
				{
					await LandLayer.SetupMapAsync(Map);
					await Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background).ConfigureAwait(false);
				}).ConfigureAwait(false);
			else
				Map = TopologyMap.LoadCollection(Properties.Resources.DefaultMap);

			OverlayLayer = new OverlayLayer(Projection)
			{
				RenderObjects = RenderObjects,
			};
			RealtimeOverlayLayer = new RealtimeOverlayLayer(Projection)
			{
				RealtimeRenderObjects = RealtimeRenderObjects,
				StandByRenderObjects = StandByRealtimeRenderObjects,
			};
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

			LandLayer?.Render(canvas, IsNavigating);
			OverlayLayer?.Render(canvas, IsNavigating);
			RealtimeOverlayLayer?.Render(canvas, IsNavigating);

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

			if (LandLayer != null)
			{
				LandLayer.LeftTopLocation = leftTop.ToLocation(Projection, zoom).CastPoint();
				LandLayer.ViewAreaRect = new RectD(LandLayer.LeftTopLocation, rightBottom.ToLocation(Projection, zoom).CastPoint());
				LandLayer.Zoom = zoom;
			}
			if (OverlayLayer != null)
			{
				OverlayLayer.LeftTopPixel = leftTop;
				OverlayLayer.PixelBound = new RectD(leftTop, rightBottom);
				OverlayLayer.Zoom = zoom;
			}
			if (RealtimeOverlayLayer != null)
			{
				RealtimeOverlayLayer.LeftTopPixel = leftTop;
				RealtimeOverlayLayer.PixelBound = new RectD(leftTop, rightBottom);
				RealtimeOverlayLayer.Zoom = zoom;
			}
		}

		public void Dispose()
		{
		}
	}
}
