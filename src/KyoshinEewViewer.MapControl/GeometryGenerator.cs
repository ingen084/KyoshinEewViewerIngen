using KyoshinEewViewer.MapControl.Projections;
using KyoshinMonitorLib;
using System;
using System.Windows.Media;

namespace KyoshinEewViewer.MapControl
{
	public static class GeometryGenerator
	{
		// Author: M-nohira
		public static Geometry MakeCircleGeometry(MapProjection projection, Location center, double radius, double zoom, int div = 90)
		{
			if (radius <= 0 || center == null)
			{
				return null;
			}

			const double EATRH_RADIUS = 6378.137;

			var pathFigure = new PathFigure
			{
				Segments = new PathSegmentCollection()
			};

			var d_rad = 2 * Math.PI / div;
			var c_lat_rad = center.Latitude / 180 * Math.PI;

			var gamma_rad = radius / 1000 / EATRH_RADIUS;
			var invert_c_lat_rad = (Math.PI / 2) - c_lat_rad;

			var cos_invert_c_rad = Math.Cos(invert_c_lat_rad);
			var cos_gamma_rad = Math.Cos(gamma_rad);
			var sin_invert_c_rad = Math.Sin(invert_c_lat_rad);
			var sin_gamma_rad = Math.Sin(gamma_rad);

			for (int count = 0; count <= div; count++)
			{
				//球面三角形における正弦余弦定理使用
				var rad = d_rad * count;
				var cos_inv_dist_lat = (cos_invert_c_rad * cos_gamma_rad) + (sin_invert_c_rad * sin_gamma_rad * Math.Cos(rad));
				var sin_d_lon = sin_gamma_rad * Math.Sin(rad) / Math.Sin(Math.Acos(cos_inv_dist_lat));

				var lat = ((Math.PI / 2) - Math.Acos(cos_inv_dist_lat)) * 180 / Math.PI;
				var lon = center.Longitude + Math.Asin(sin_d_lon) * 180 / Math.PI;
				var loc = new Location((float)lat, (float)lon);

				if (count == 0)
					pathFigure.StartPoint = loc.ToPixel(projection, zoom);
				else
				{
					var segment = new LineSegment(loc.ToPixel(projection, zoom), true);
					segment.Freeze();
					pathFigure.Segments.Add(segment);
				}
			}

			pathFigure.Segments.Freeze();
			pathFigure.Freeze();
			var pathFigures = new PathFigureCollection
			{
				pathFigure
			};
			pathFigures.Freeze();
			return new PathGeometry(pathFigures);
		}
	}
}
