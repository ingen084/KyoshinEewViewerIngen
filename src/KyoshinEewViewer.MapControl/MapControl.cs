using KyoshinMonitorLib;
using System;
using System.Windows;
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
				if (value < 0 || value > MaxZoomLevel)
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

		#region CenterLocation
		public static readonly DependencyProperty CenterLocationProperty
			= DependencyProperty.Register("CenterLocation", typeof(Location), typeof(MapControl), new UIPropertyMetadata(new Location(0, 0)));

		public Location CenterLocation
		{
			set
			{
				SetValue(CenterLocationProperty, value);
				Render();
			}
			get => (Location)GetValue(CenterLocationProperty);
		}
		#endregion

		#region Background
		public Brush Background
		{
			get => (Brush)GetValue(BackgroundProperty);
			set
			{
				SetValue(BackgroundProperty, value);
				Render();
			}
		}

		// Using a DependencyProperty as the backing store for Background.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty BackgroundProperty =
			DependencyProperty.Register("Background", typeof(Brush), typeof(MapControl), new PropertyMetadata(Brushes.White));
		#endregion

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
			if (Background != null)
				drawingContext.DrawRectangle(Background, null, new Rect(0.0, 0.0, RenderSize.Width, RenderSize.Height));

			var halfRenderSize = new Vector(RenderSize.Width / 2, RenderSize.Height / 2);
			// 左上/右下のピクセル座標
			var leftTop = CenterLocation.ToPixel(Zoom) - halfRenderSize;
			var rightBottom = CenterLocation.ToPixel(Zoom) + halfRenderSize;

			var pen = new Pen(Brushes.DarkGray, 1.2);
			var pen2 = new Pen(Brushes.Gray, 1);
			var landBrush = new SolidColorBrush(Color.FromRgb(62, 62, 66));

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
							drawingContext.DrawGeometry(null, pen, geometry);
							break;
						case FeatureType.AdminBoundary:
							drawingContext.DrawGeometry(null, pen2, geometry);
							break;
						case FeatureType.Polygon:
							drawingContext.DrawGeometry(landBrush, null, geometry);
							break;
					}
				}

			//drawingContext.DrawEllipse(Brushes.Red, null, new Point() + halfRenderSize, 2, 2);

			base.OnRender(drawingContext);
		}
	}
}
