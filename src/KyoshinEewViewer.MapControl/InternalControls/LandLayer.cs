using System;
using System.Windows;
using System.Windows.Media;

namespace KyoshinEewViewer.MapControl.InternalControls
{
	internal sealed class LandLayer : MapLayerBase
	{
		public Point LeftTopLocation { get; set; }
		public Rect ViewAreaRect { get; set; }
		public FeatureCacheController Controller { get; set; }

		protected override void OnRender(DrawingContext drawingContext)
		{
			if (Controller == null)
				return;

			var coastlineStroke = new Pen((Brush)FindResource("LandStrokeColor"), (double)FindResource("LandStrokeThickness"));
			coastlineStroke.Freeze();
			var adminBoundStroke = new Pen((Brush)FindResource("PrefStrokeColor"), (double)FindResource("PrefStrokeThickness"));
			adminBoundStroke.Freeze();
			var landFill = (Brush)FindResource("LandColor");
			var invalidateLandStroke = (double)FindResource("LandStrokeThickness") <= 0;
			var invalidatePrefStroke = (double)FindResource("PrefStrokeThickness") <= 0;

			var rZoom = (int)Math.Ceiling(Zoom);
			var dZoom = Math.Pow(2, Zoom - rZoom);

			var leftTop = LeftTopLocation.CastLocation().ToPixel(Projection, rZoom);

			var transform = new TransformGroup
			{
				Children = new TransformCollection(new Transform[]
					{
						new TranslateTransform(-leftTop.X, -leftTop.Y),
						new ScaleTransform(dZoom, dZoom),
					})
			};
			transform.Freeze();

			var landGeometry = new StreamGeometry() { Transform = transform };
			var prefGeometry = new StreamGeometry() { Transform = transform };
			var clineGeometry = new StreamGeometry() { Transform = transform };
			using (var landStream = landGeometry.Open())
			using (var prefStream = prefGeometry.Open())
			using (var clineStream = clineGeometry.Open())
			{
				foreach (var f in Controller.Find(ViewAreaRect))
				{
					switch (f.Type)
					{
						case FeatureType.Polygon:
							f.AddFigure(landStream, Projection, rZoom);
							break;
						case FeatureType.AdminBoundary:
						case FeatureType.SubAdminBoundary:
							if (!invalidatePrefStroke)
								f.AddFigure(prefStream, Projection, rZoom);
							break;
						case FeatureType.Coastline:
							if (!invalidateLandStroke)
								f.AddFigure(clineStream, Projection, rZoom);
							break;
					}
				}
			}
			landGeometry.Freeze();
			clineGeometry.Freeze();
			prefGeometry.Freeze();

			drawingContext.DrawGeometry(landFill, null, landGeometry);
			if (!invalidateLandStroke)
				drawingContext.DrawGeometry(null, coastlineStroke, clineGeometry);
			if (!invalidatePrefStroke)
				drawingContext.DrawGeometry(null, adminBoundStroke, prefGeometry);
		}
	}
}
