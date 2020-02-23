using KyoshinEewViewer.MapControl.InternalControls;
using KyoshinEewViewer.MapControl.RenderObjects;
using KyoshinMonitorLib;
using System;
using System.Windows;
using System.Windows.Controls;

namespace KyoshinEewViewer.MapControl
{
	public sealed class MapControl : Grid
	{
		#region MaxZoomLevel
		public static readonly DependencyProperty MaxZoomLevelProperty
			= DependencyProperty.Register("MaxZoomLevel", typeof(double), typeof(MapControl), new UIPropertyMetadata(20d));

		public double MaxZoomLevel
		{
			set
			{
				if (value < 0)
					return;
				SetValue(MaxZoomLevelProperty, value);
			}
			get => (double)GetValue(MaxZoomLevelProperty);
		}
		#endregion
		#region MinZoomLevel
		public static readonly DependencyProperty MinZoomLevelProperty
			= DependencyProperty.Register("MinZoomLevel", typeof(double), typeof(MapControl), new UIPropertyMetadata(0d));

		public double MinZoomLevel
		{
			set
			{
				if (value < 0)
					return;
				SetValue(MinZoomLevelProperty, value);
			}
			get => (double)GetValue(MinZoomLevelProperty);
		}
		#endregion
		#region ZoomProperty
		public static readonly DependencyProperty ZoomProperty
			= DependencyProperty.Register("Zoom", typeof(double), typeof(MapControl), new UIPropertyMetadata(0d, (s, e) =>
			{
				if (s is MapControl map)
				{
					if (map.LandRender != null)
						map.LandRender.Zoom = (double)e.NewValue;
					if (map.OverlayRender != null)
						map.OverlayRender.Zoom = (double)e.NewValue;
					map.ApplySize();
					map.InvalidateChildVisual();
				}
			}));
		public double Zoom
		{
			set
			{
				if (value < 0 || value > MaxZoomLevel || value < MinZoomLevel)
					return;
				SetValue(ZoomProperty, value);
			}
			get => (double)GetValue(ZoomProperty);
		}
		#endregion
		#region CenterLocation
		public static readonly DependencyProperty CenterLocationProperty
			= DependencyProperty.Register("CenterLocation", typeof(Location), typeof(MapControl), new UIPropertyMetadata(new Location(0, 0), (s, e) =>
			{
				if (s is MapControl map)
				{
					map.ApplySize();
					map.InvalidateChildVisual();
				}
			}));
		public Location CenterLocation
		{
			set => SetValue(CenterLocationProperty, value);
			get => (Location)GetValue(CenterLocationProperty);
		}
		#endregion
		#region Padding
		public Thickness Padding
		{
			get => (Thickness)GetValue(PaddingProperty);
			set => SetValue(PaddingProperty, value);
		}
		public static readonly DependencyProperty PaddingProperty
			= DependencyProperty.Register("Padding", typeof(Thickness), typeof(MapControl), new PropertyMetadata(new Thickness(), (s, e) =>
			{
				if (s is MapControl map)
				{
					map.ApplySize();
					map.InvalidateChildVisual();
				}
			}));
		#endregion
		#region RenderObjects
		public RenderObject[] RenderObjects
		{
			get => (RenderObject[])GetValue(RenderObjectsProperty);
			set => SetValue(RenderObjectsProperty, value);
		}
		public static readonly DependencyProperty RenderObjectsProperty =
			DependencyProperty.Register("RenderObjects", typeof(RenderObject[]), typeof(MapControl), new PropertyMetadata(null, (s, e) =>
			{
				if (s is MapControl map && map.OverlayRender != null)
				{
					map.OverlayRender.RenderObjects = (RenderObject[])e.NewValue;
					map.OverlayRender.InvalidateVisual();
				}
			}));
		#endregion
		#region Map
		public TopologyMap Map
		{
			get => (TopologyMap)GetValue(MapProperty);
			set => SetValue(MapProperty, value);
		}
		public static readonly DependencyProperty MapProperty =
			DependencyProperty.Register("Map", typeof(TopologyMap), typeof(MapControl), new PropertyMetadata(null, (s, e) =>
			{
				if (s is MapControl map && map.LandRender != null)
				{
					map.LandRender.Controller = new FeatureCacheController((TopologyMap)e.NewValue);
					map.LandRender.InvalidateVisual();
				}
			}));
		#endregion

		public Rect PaddedRect { get; private set; }

		static MapControl()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(MapControl), new FrameworkPropertyMetadata(typeof(MapControl)));
		}

		private LandRender LandRender { get; set; }
		private OverlayRender OverlayRender { get; set; }
		protected override void OnInitialized(EventArgs e)
		{
			Children.Add(LandRender = new LandRender
			{
				Zoom = Zoom,
				CenterLocation = CenterLocation,
			});
			if (Map != null)
				LandRender.Controller = new FeatureCacheController(Map);
			Children.Add(OverlayRender = new OverlayRender
			{
				Zoom = Zoom,
				CenterLocation = CenterLocation,
				RenderObjects = RenderObjects,
			});
			ApplySize();

			base.OnInitialized(e);
		}
		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
		{
			ApplySize();
			base.OnRenderSizeChanged(sizeInfo);
			InvalidateChildVisual();
		}
		private void ApplySize()
		{
			// DP Cache
			var renderSize = RenderSize;
			var padding = Padding;
			PaddedRect = new Rect(new Point(padding.Left, padding.Top), new Point(Math.Max(0, renderSize.Width - padding.Right), Math.Max(0, renderSize.Height - padding.Bottom)));
			var zoom = Zoom;
			var centerLocation = CenterLocation ?? new Location(0, 0); // null island

			var halfRenderSize = new Vector(PaddedRect.Width / 2, PaddedRect.Height / 2);
			// 左上/右下のピクセル座標
			var leftTop = centerLocation.ToPixel(zoom) - halfRenderSize - new Vector(padding.Left, padding.Top);
			var rightBottom = centerLocation.ToPixel(zoom) + halfRenderSize + new Vector(padding.Right, padding.Bottom);

			if (LandRender != null)
			{
				LandRender.LeftTopLocation = leftTop.ToLocation(zoom).AsPoint();
				LandRender.ViewAreaRect = new Rect(LandRender.LeftTopLocation, rightBottom.ToLocation(zoom).AsPoint());
			}
			if (OverlayRender != null)
			{
				OverlayRender.LeftTopPixel = leftTop;
				OverlayRender.PixelBound = new Rect(leftTop, rightBottom);
			}
		}

		public void InvalidateChildVisual()
		{
			LandRender?.InvalidateVisual();
			OverlayRender?.InvalidateVisual();
		}

		// 指定した範囲をすべて表示できるように調整する
		public void Navigate(Rect bound)
		{
			var boundPixel = new Rect(bound.TopLeft.AsLocation().ToPixel(Zoom), bound.BottomRight.AsLocation().ToPixel(Zoom));

			var scale = new Point(PaddedRect.Width / boundPixel.Width, PaddedRect.Height / boundPixel.Height);

			CenterLocation = new Point(boundPixel.Left + boundPixel.Width / 2, boundPixel.Top + boundPixel.Height / 2).ToLocation(Zoom);
			Zoom += Math.Log(Math.Min(scale.X, scale.Y), 2);
		}
	}
}
