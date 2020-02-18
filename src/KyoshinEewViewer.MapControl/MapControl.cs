using KyoshinMonitorLib;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace KyoshinEewViewer.MapControl
{
	public class MapControl : FrameworkElement
	{
		#region ZoomProperty
		public static readonly DependencyProperty ZoomProperty
			= DependencyProperty.Register("Zoom", typeof(double), typeof(MapControl), new UIPropertyMetadata(0d, (s, e) =>
			{
				if (!(s is MapControl))
					return;

				var map = s as MapControl;
				map.Render();
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
			= DependencyProperty.Register("CenterLocation", typeof(Location), typeof(MapControl), new UIPropertyMetadata(new Location(0, 0), (s,e) =>
			{
				if (!(s is MapControl))
					return;

				var map = s as MapControl;
				map.Render();
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
			set
			{
				SetValue(PaddingProperty, value);
				Render();
			}
		}

		// Using a DependencyProperty as the backing store for Padding.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty PaddingProperty =
			DependencyProperty.Register("Padding", typeof(Thickness), typeof(MapControl), new PropertyMetadata(new Thickness()));
		#endregion

		public Rect PaddedRect => new Rect(Padding.Left, Padding.Top, Math.Max(0, RenderSize.Width - Padding.Right - Padding.Left), Math.Max(0, RenderSize.Height - Padding.Bottom - Padding.Top));


		FeatureCacheController Controller { get; set; }
		static MapControl()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(MapControl), new FrameworkPropertyMetadata(typeof(MapControl)));
		}

		protected override async void OnInitialized(EventArgs e)
		{
			base.OnInitialized(e);

			//TODO: 応急処置
			Controller = new FeatureCacheController(await TopologyMap.LoadAsync(@"japan_map_m.mpk.lz4"));
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
			var halfRenderSize = new Vector(paddedRect.Width / 2, paddedRect.Height / 2);
			// 左上/右下のピクセル座標
			var leftTop = CenterLocation.ToPixel(Zoom) - halfRenderSize - new Vector(Padding.Left, Padding.Top);
			var rightBottom = CenterLocation.ToPixel(Zoom) + halfRenderSize + new Vector(Padding.Right, Padding.Bottom);

			var coastlineStroke = new Pen((Brush)FindResource("LandStrokeColor"), (double)FindResource("LandStrokeThickness"));
			var adminBoundStroke = new Pen((Brush)FindResource("PrefStrokeColor"), (double)FindResource("PrefStrokeThickness"));
			var landFill = (Brush)FindResource("LandColor");

			if (Controller != null)
				foreach (var f in Controller.Find(new Rect(leftTop.ToLocation(Zoom).AsPoint(), rightBottom.ToLocation(Zoom).AsPoint())))
				{
					var geometry = f.CreateGeometry(Zoom);
					if (geometry == null)
						continue;
					var translate = new Point() - leftTop;
					geometry.Transform = new TranslateTransform(translate.X, translate.Y);
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

			drawingContext.DrawEllipse(Brushes.Red, null, new Point(paddedRect.Left + paddedRect.Width / 2, paddedRect.Top + paddedRect.Height / 2), 2, 2);
#if DEBUG
			drawingContext.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(100, 200, 0, 0)), 1), paddedRect);
#endif

			base.OnRender(drawingContext);
		}
	}
}
