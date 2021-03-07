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

				ApplySize();
				Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background).ConfigureAwait(false);
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
				ApplySize();
				Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background).ConfigureAwait(false);
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

		private IRenderObject[]? renderObjects;
		public IRenderObject[]? RenderObjects
		{
			get => renderObjects;
			set
			{
				if (renderObjects == value)
					return;
				renderObjects = value;
				if (OverlayLayer != null)
					OverlayLayer.RenderObjects = renderObjects;
				Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background).ConfigureAwait(false);
			}
		}
		private RealtimeRenderObject[]? realtimeRenderObjects;
		public RealtimeRenderObject[]? RealtimeRenderObjects
		{
			get => realtimeRenderObjects;
			set
			{
				if (realtimeRenderObjects == value)
					return;
				realtimeRenderObjects = value;
				if (RealtimeOverlayLayer != null)
					RealtimeOverlayLayer.RealtimeRenderObjects = realtimeRenderObjects;
				Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background).ConfigureAwait(false);
			}
		}

		public Rect Padding { get; set; } = new Rect(0, 0, 0, 0);

		public MapProjection Projection { get; set; } = new MillerProjection();

		public void RefleshResourceCache()
		{
			LandLayer?.RefleshResourceCache(this);
			OverlayLayer?.RefleshResourceCache(this);
			InvalidateVisual();
		}

		// 指定した範囲をすべて表示できるように調整する
		public void Navigate(RectD bound)
		{
			var boundPixel = new RectD(bound.BottomLeft.CastLocation().ToPixel(Projection, Zoom), bound.TopRight.CastLocation().ToPixel(Projection, Zoom));
			var centerPixel = CenterLocation.ToPixel(Projection, Zoom);
			var halfRect = new PointD(PaddedRect.Width / 2, PaddedRect.Height / 2);
			var leftTop = centerPixel - halfRect;
			var rightBottom = centerPixel + halfRect;
			Navigate(new NagivateAnimationParameter(
					Zoom,
					new RectD(leftTop, rightBottom),
					boundPixel));
		}
		internal void Navigate(NagivateAnimationParameter parameter)
		{
			var boundPixel = new RectD(parameter.ToRect.TopLeft, parameter.ToRect.BottomRight);
			var scale = new PointD(PaddedRect.Width / boundPixel.Width, PaddedRect.Height / boundPixel.Height);
			var relativeZoom = Math.Log(Math.Min(scale.X, scale.Y), 2);
			CenterLocation = new PointD(
				boundPixel.Left + boundPixel.Width / 2,
				boundPixel.Top + boundPixel.Height / 2).ToLocation(Projection, Zoom);
			Zoom += relativeZoom;
			return;
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
			RealtimeOverlayLayer = new RealtimeOverlayLayer(Projection, this)
			{
				RealtimeRenderObjects = RealtimeRenderObjects,
			};
			ApplySize();

			//NavigateAnimation.EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut };
			//NavigateAnimation.Completed += (s, e) =>
			//{
			//	AnimationParameter = null;
			//	NavigateAnimation.BeginTime = null;
			//};
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

			LandLayer?.OnRender(canvas, Zoom);
			OverlayLayer?.OnRender(canvas, Zoom);
			RealtimeOverlayLayer?.OnRender(canvas, Zoom);

			canvas.Restore();
		}

		public override void Render(DrawingContext context)
		{
			context.Custom(this);
			// Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
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
			}
			if (OverlayLayer != null)
			{
				OverlayLayer.LeftTopPixel = leftTop;
				OverlayLayer.PixelBound = new RectD(leftTop, rightBottom);
			}
			if (RealtimeOverlayLayer != null)
			{
				RealtimeOverlayLayer.LeftTopPixel = leftTop;
				RealtimeOverlayLayer.PixelBound = new RectD(leftTop, rightBottom);
			}
		}

		public void Dispose()
		{
		}
	}
}
