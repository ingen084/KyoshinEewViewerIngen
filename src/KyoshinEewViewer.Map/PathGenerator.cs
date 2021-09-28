using KyoshinEewViewer.Map.Projections;
using KyoshinMonitorLib;
using SkiaSharp;
using System;

namespace KyoshinEewViewer.Map
{
	public static class PathGenerator
	{
		// Author: M-nohira
		public static SKPath? MakeCirclePath(MapProjection projection, Location? center, double radius, double zoom, int div = 90, SKPath? basePath = null)
		{
			if (radius <= 0 || center == null)
				return null;

			const double EATRH_RADIUS = 6371;

			var path = basePath ?? new SKPath();

			var d_rad = 2 * Math.PI / div;
			var c_lat_rad = center.Latitude / 180 * Math.PI;

			var gamma_rad = radius / 1000 / EATRH_RADIUS;
			var invert_c_lat_rad = (Math.PI / 2) - c_lat_rad;

			var cos_invert_c_rad = Math.Cos(invert_c_lat_rad);
			var cos_gamma_rad = Math.Cos(gamma_rad);
			var sin_invert_c_rad = Math.Sin(invert_c_lat_rad);
			var sin_gamma_rad = Math.Sin(gamma_rad);

			for (var count = 0; count <= div; count++)
			{
				//球面三角形における正弦余弦定理使用
				var rad = d_rad * count;
				var cos_inv_dist_lat = (cos_invert_c_rad * cos_gamma_rad) + (sin_invert_c_rad * sin_gamma_rad * Math.Cos(rad));
				var sin_d_lon = sin_gamma_rad * Math.Sin(rad) / Math.Sin(Math.Acos(cos_inv_dist_lat));

				var lat = ((Math.PI / 2) - Math.Acos(cos_inv_dist_lat)) * 180 / Math.PI;
				var lon = center.Longitude + Math.Asin(sin_d_lon) * 180 / Math.PI;
				var loc = new Location((float)lat, (float)lon);

				if (count == 0)
					path.MoveTo(loc.ToPixel(projection, zoom).AsSKPoint());
				else
					path.LineTo(loc.ToPixel(projection, zoom).AsSKPoint());
			}
			path.Close();
			return path;
		}
	}
}
