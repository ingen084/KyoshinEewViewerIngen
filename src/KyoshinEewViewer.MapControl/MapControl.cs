using KyoshinEewViewer.MapControl.RenderObjects;
using KyoshinMonitorLib;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace KyoshinEewViewer.MapControl
{
	public class MapControl : Grid
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

		//FeatureCacheController Controller { get; set; }
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
				Controller = new FeatureCacheController(Map),
			});
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
			var paddedRect = new Rect(new Point(padding.Left, padding.Top), new Point(Math.Max(0, renderSize.Width - padding.Right), Math.Max(0, renderSize.Height - padding.Bottom)));
			var zoom = Zoom;
			var centerLocation = CenterLocation ?? new Location(0, 0); // null island

			PaddedRect = paddedRect;

			var halfRenderSize = new Vector(paddedRect.Width / 2, paddedRect.Height / 2);
			// 左上/右下のピクセル座標
			var leftTop = centerLocation.ToPixel(zoom) - halfRenderSize - new Vector(padding.Left, padding.Top);
			var rightBottom = centerLocation.ToPixel(zoom) + halfRenderSize + new Vector(padding.Right, padding.Bottom);

			if (LandRender != null)
			{
				LandRender.LeftTop = leftTop;
				LandRender.ViewAreaRect = new Rect(leftTop.ToLocation(zoom).AsPoint(), rightBottom.ToLocation(zoom).AsPoint());
			}
			if (OverlayRender != null)
			{
				OverlayRender.LeftTop = leftTop;
				OverlayRender.PixelBound = new Rect(leftTop, rightBottom);
			}
		}

		public void InvalidateChildVisual()
		{
			LandRender?.InvalidateVisual();
			OverlayRender?.InvalidateVisual();
		}
	}
	internal class MapRenderBase : FrameworkElement
	{
		public double Zoom { get; set; }
		public Location CenterLocation { get; set; }
		public Point LeftTop { get; set; }
	}
	internal class LandRender : MapRenderBase
	{
		public Rect ViewAreaRect { get; set; }
		public FeatureCacheController Controller { get; set; }

		protected override void OnRender(DrawingContext drawingContext)
		{
			var coastlineStroke = new Pen((Brush)FindResource("LandStrokeColor"), (double)FindResource("LandStrokeThickness"));
			var adminBoundStroke = new Pen((Brush)FindResource("PrefStrokeColor"), (double)FindResource("PrefStrokeThickness"));
			var landFill = (Brush)FindResource("LandColor");

			if (Controller != null)
				foreach (var f in Controller.Find(ViewAreaRect))
				{
					var geometry = f.CreateGeometry(Zoom);
					if (geometry == null)
						continue;

					if (geometry.Transform is TranslateTransform tt)
					{
						tt.X = -LeftTop.X;
						tt.Y = -LeftTop.Y;
					}
					else
						geometry.Transform = new TranslateTransform(-LeftTop.X, -LeftTop.Y);

					switch (f.Type)
					{
						case FeatureType.Coastline:
							if ((double)FindResource("LandStrokeThickness") <= 0)
								break;
							drawingContext.DrawGeometry(null, coastlineStroke, geometry);
							break;
						case FeatureType.AdminBoundary:
							if ((double)FindResource("PrefStrokeThickness") <= 0)
								break;
							drawingContext.DrawGeometry(null, adminBoundStroke, geometry);
							break;
						case FeatureType.Polygon:
							drawingContext.DrawGeometry(landFill, null, geometry);
							break;
					}
				}
		}
	}
	internal class OverlayRender : MapRenderBase
	{
		public Rect PixelBound { get; set; }
		public RenderObject[] RenderObjects { get; set; }

		protected override void OnRender(DrawingContext drawingContext)
		{
			if (RenderObjects != null)
				foreach (var o in RenderObjects)
					lock (o)
						o.Render(drawingContext, PixelBound, Zoom, LeftTop);
		}
	}
}
