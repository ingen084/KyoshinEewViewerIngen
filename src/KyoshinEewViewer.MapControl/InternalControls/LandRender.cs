using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace KyoshinEewViewer.MapControl.InternalControls
{
	internal sealed class LandRender : MapRenderBase
	{
		public Point LeftTopLocation { get; set; }
		public Rect ViewAreaRect { get; set; }
		public FeatureCacheController Controller { get; set; }

		protected override void OnRender(DrawingContext drawingContext)
		{
			if (Controller == null)
				return;

			var coastlineStroke = new Pen((Brush)FindResource("LandStrokeColor"), (double)FindResource("LandStrokeThickness"));
			var adminBoundStroke = new Pen((Brush)FindResource("PrefStrokeColor"), (double)FindResource("PrefStrokeThickness"));
			var landFill = (Brush)FindResource("LandColor");

			var rZoom = (int)Math.Floor(Zoom);
			var dZoom = Math.Pow(2, Zoom - rZoom);

			var leftTop = LeftTopLocation.AsLocation().ToPixel(rZoom);

			foreach (var f in Controller.Find(ViewAreaRect))
			{
				var geometry = f.CreateGeometry(rZoom);
				if (geometry == null)
					continue;

				if (geometry.Transform is TransformGroup tg)
				{
					var tt = tg.Children[0] as TranslateTransform;
					tt.X = -leftTop.X;
					tt.Y = -leftTop.Y;

					var st = tg.Children[1] as ScaleTransform;
					st.ScaleX = st.ScaleY = dZoom;
				}
				else
					geometry.Transform = new TransformGroup
					{
						Children = new TransformCollection(new Transform[]
						{
							new TranslateTransform(-leftTop.X, -leftTop.Y),
							new ScaleTransform(dZoom, dZoom),
						})
					};

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
}
