using KyoshinEewViewer.MapControl.RenderObjects;
using KyoshinMonitorLib;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace KyoshinEewViewer.MapControl
{
	public class MapControl : FrameworkElement
	{
		#region ZoomProperty
		public static readonly DependencyProperty ZoomProperty
			= DependencyProperty.Register("Zoom", typeof(double), typeof(MapControl), new UIPropertyMetadata(0d, (s, e) => (s as MapControl).Render()));

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
		#region CenterLocation
		public static readonly DependencyProperty CenterLocationProperty
			= DependencyProperty.Register("CenterLocation", typeof(Location), typeof(MapControl), new UIPropertyMetadata(new Location(0, 0), (s, e) => (s as MapControl).Render()));

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

		// Using a DependencyProperty as the backing store for Padding.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty PaddingProperty =
			DependencyProperty.Register("Padding", typeof(Thickness), typeof(MapControl), new PropertyMetadata(new Thickness(), (s, e) => (s as MapControl).Render()));
		#endregion
		#region RenderObjects
		public IEnumerable<RenderObject> RenderObjects
		{
			get => (IEnumerable<RenderObject>)GetValue(RenderObjectsProperty);
			set => SetValue(RenderObjectsProperty, value);
		}
		public static readonly DependencyProperty RenderObjectsProperty =
			DependencyProperty.Register("RenderObjects", typeof(IEnumerable<RenderObject>), typeof(MapControl), new PropertyMetadata(null, (s, e) => (s as MapControl).Render()));
		#endregion

		public Rect PaddedRect
		{
			get
			{
				var padding = Padding;
				var renderSize = RenderSize;
				return new Rect(padding.Left, padding.Top, Math.Max(0, renderSize.Width - padding.Right - padding.Left), Math.Max(0, renderSize.Height - padding.Bottom - padding.Top));
			}
		}

		FeatureCacheController Controller { get; set; }
		static MapControl()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(MapControl), new FrameworkPropertyMetadata(typeof(MapControl)));
		}

		public void InitalizeAsync(TopologyMap map)
		{
			Controller = new FeatureCacheController(map);
			Render();
		}

		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
		{
			base.OnRenderSizeChanged(sizeInfo);
			Render();
		}

		readonly DrawingGroup backingStore = new DrawingGroup();
		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);

			Render();
			drawingContext.DrawDrawing(backingStore);
		}

		bool isRendering = false;
		public void Render()
		{
			if (isRendering)
				return;
			isRendering = true;
			var drawingContext = backingStore.Open();
			Render(drawingContext);
			drawingContext.Close();
			isRendering = false;
		}
		void Render(DrawingContext drawingContext)
		{
			var paddedRect = PaddedRect;
			var zoom = Zoom;
			var padding = Padding;
			var halfRenderSize = new Vector(paddedRect.Width / 2, paddedRect.Height / 2);
			// 左上/右下のピクセル座標
			var leftTop = CenterLocation.ToPixel(zoom) - halfRenderSize - new Vector(padding.Left, padding.Top);
			var rightBottom = CenterLocation.ToPixel(zoom) + halfRenderSize + new Vector(padding.Right, padding.Bottom);

			var coastlineStroke = new Pen((Brush)FindResource("LandStrokeColor"), (double)FindResource("LandStrokeThickness"));
			var adminBoundStroke = new Pen((Brush)FindResource("PrefStrokeColor"), (double)FindResource("PrefStrokeThickness"));
			var landFill = (Brush)FindResource("LandColor");

			var viewAreaRect = new Rect(leftTop.ToLocation(zoom).AsPoint(), rightBottom.ToLocation(zoom).AsPoint());

			if (Controller != null)
				foreach (var f in Controller.Find(viewAreaRect))
				{
					var geometry = f.CreateGeometry(zoom);
					if (geometry == null)
						continue;
					geometry.Transform = new TranslateTransform(-leftTop.X, -leftTop.Y);
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

			// TODO: きちんとヒットチェックをする
			if (RenderObjects != null)
				foreach (var o in RenderObjects)
					o.Render(drawingContext, paddedRect, zoom, leftTop);

#if DEBUG
			var debugPen = new Pen(Brushes.Red, 1);
			drawingContext.DrawLine(debugPen, new Point(paddedRect.Left, paddedRect.Top), new Point(paddedRect.Left + paddedRect.Width, paddedRect.Top + paddedRect.Height));
			drawingContext.DrawLine(debugPen, new Point(paddedRect.Left + paddedRect.Width, paddedRect.Top), new Point(paddedRect.Left, paddedRect.Top + paddedRect.Height));
			//drawingContext.DrawLine(debugPen, new Point(paddedRect.Left + paddedRect.Width / 2 - 5, paddedRect.Top + paddedRect.Height / 2 - 5), new Point(paddedRect.Left + paddedRect.Width / 2 + 5, paddedRect.Top + paddedRect.Height / 2 + 5));
			//drawingContext.DrawLine(debugPen, new Point(paddedRect.Left + paddedRect.Width / 2 + 5, paddedRect.Top + paddedRect.Height / 2 - 5), new Point(paddedRect.Left + paddedRect.Width / 2 - 5, paddedRect.Top + paddedRect.Height / 2 + 5));
			drawingContext.DrawRectangle(null, debugPen, paddedRect);
#endif

			base.OnRender(drawingContext);
		}
	}
}
