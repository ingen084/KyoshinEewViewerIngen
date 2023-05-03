using KyoshinMonitorLib;
using SkiaSharp;
using System;

namespace KyoshinEewViewer.Map;

public static class PathGenerator
{
	// Author: M-nohira
	public static SKPath MakeCirclePath(Location center, double radius, double zoom, int div = 90, SKPath? basePath = null)
	{
		if (radius <= 0)
			throw new ArgumentOutOfRangeException(nameof(radius));

		const double eatrhRadius = 6371;

		var path = basePath ?? new SKPath();

		var dRad = 2 * Math.PI / div;
		var cLatRad = center.Latitude / 180 * Math.PI;

		var gammaRad = radius / 1000 / eatrhRadius;
		var invertCLatRad = (Math.PI / 2) - cLatRad;

		var cosInvertCRad = Math.Cos(invertCLatRad);
		var cosGammaRad = Math.Cos(gammaRad);
		var sinInvertCRad = Math.Sin(invertCLatRad);
		var sinGammaRad = Math.Sin(gammaRad);

		var prevPoint = new SKPoint();

		for (var count = 0; count <= div; count++)
		{
			//球面三角形における正弦余弦定理使用
			var rad = dRad * count;
			var cosInvDistLat = (cosInvertCRad * cosGammaRad) + (sinInvertCRad * sinGammaRad * Math.Cos(rad));
			var sinDLon = sinGammaRad * Math.Sin(rad) / Math.Sin(Math.Acos(cosInvDistLat));

			var lat = ((Math.PI / 2) - Math.Acos(cosInvDistLat)) * 180 / Math.PI;
			var lon = center.Longitude + Math.Asin(sinDLon) * 180 / Math.PI;
			var loc = new Location((float)lat, (float)lon);

			var point = loc.ToPixel(zoom).AsSkPoint();

			if (count == 0)
				path.MoveTo(point);
			else if (count % 2 == 0 && prevPoint != default)
				path.QuadTo(prevPoint, point);
			else
				path.LineTo(point);

			prevPoint = point;
		}
		path.Close();
		return path;
	}
}
