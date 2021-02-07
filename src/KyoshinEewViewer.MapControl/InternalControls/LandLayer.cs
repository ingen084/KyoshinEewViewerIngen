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

		#region ResourceCache
		private Pen CoastlineStroke { get; set; }
		private Pen PrefStroke { get; set; }
		private Pen AreaStroke { get; set; }

		private Brush LandFill { get; set; }
		private Brush OverSeasLandFill { get; set; }

		private bool InvalidateLandStroke { get; set; }
		private bool InvalidatePrefStroke { get; set; }
		private bool InvalidateAreaStroke { get; set; }

		public void RefleshResourceCache()
		{
			CoastlineStroke = new Pen((Brush)FindResource("LandStrokeColor"), (double)FindResource("LandStrokeThickness"));
			CoastlineStroke.Freeze();
			PrefStroke = new Pen((Brush)FindResource("PrefStrokeColor"), (double)FindResource("PrefStrokeThickness"));
			PrefStroke.Freeze();
			AreaStroke = new Pen((Brush)FindResource("AreaStrokeColor"), (double)FindResource("AreaStrokeThickness"));
			AreaStroke.Freeze();
			LandFill = (Brush)FindResource("LandColor");
			LandFill.Freeze();
			OverSeasLandFill = (Brush)FindResource("OverseasLandColor");
			OverSeasLandFill.Freeze();
			InvalidateLandStroke = (double)FindResource("LandStrokeThickness") <= 0;
			InvalidatePrefStroke = (double)FindResource("PrefStrokeThickness") <= 0;
			InvalidateAreaStroke = (double)FindResource("AreaStrokeThickness") <= 0;
		}
		#endregion

		protected override void OnRender(DrawingContext drawingContext)
		{
			if (Controller == null)
				return;

			var rZoom = (int)Math.Ceiling(Zoom);
			var dZoom = Math.Pow(2, Zoom - rZoom);

			var invalidateAreaStroke = InvalidateAreaStroke || rZoom <= 6;

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
			var overSeasLandGeometry = new StreamGeometry() { Transform = transform };
			var prefGeometry = new StreamGeometry() { Transform = transform };
			var clineGeometry = new StreamGeometry() { Transform = transform };
			var areaGeometry = new StreamGeometry() { Transform = transform };
			using (var landStream = landGeometry.Open())
			using (var overSeasLandStream = overSeasLandGeometry.Open())
			using (var prefStream = prefGeometry.Open())
			using (var clineStream = clineGeometry.Open())
			using (var areaStream = areaGeometry.Open())
			{
				foreach (var f in Controller.Find(ViewAreaRect))
				{
					switch (f.Type)
					{
						case FeatureType.Polygon:
							f.AddFigure(f.IsOverseas ? overSeasLandStream : landStream, Projection, rZoom);
							break;
						case FeatureType.AdminBoundary:
							if (!InvalidatePrefStroke && !f.IsOverseas)
								f.AddFigure(prefStream, Projection, rZoom);
							break;
						case FeatureType.Coastline:
							if (!InvalidateLandStroke && !f.IsOverseas)
								f.AddFigure(clineStream, Projection, rZoom);
							break;
						case FeatureType.AreaBoundary:
							if (!invalidateAreaStroke && !f.IsOverseas)
								f.AddFigure(areaStream, Projection, rZoom);
							break;
					}
				}
			}
			landGeometry.Freeze();
			overSeasLandGeometry.Freeze();
			clineGeometry.Freeze();
			prefGeometry.Freeze();
			areaGeometry.Freeze();

			drawingContext.DrawGeometry(LandFill, null, landGeometry);
			drawingContext.DrawGeometry(OverSeasLandFill, null, overSeasLandGeometry);
			if (!InvalidateLandStroke)
				drawingContext.DrawGeometry(null, CoastlineStroke, clineGeometry);
			if (!InvalidatePrefStroke)
				drawingContext.DrawGeometry(null, PrefStroke, prefGeometry);
			if (!invalidateAreaStroke)
				drawingContext.DrawGeometry(null, AreaStroke, areaGeometry);
		}
	}
}
