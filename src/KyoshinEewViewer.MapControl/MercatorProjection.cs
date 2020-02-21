using KyoshinMonitorLib;
using System;
using System.Windows;

namespace KyoshinEewViewer.MapControl
{
	public static class MercatorProjection
	{
		public const int TileSize = 256;
		static readonly double PixelsPerLonDegree = TileSize / (double)360;
		static readonly double PixelsPerLonRadian = TileSize / (2 * Math.PI);
		static Point Origin = new Point(128, 128);

		public static Point LatLngToPoint(Location location)
		{
			var point = new Point()
			{
				X = Origin.X + location.Longitude * PixelsPerLonDegree
			};
			var siny = Math.Min(Math.Max(Math.Sin(DegreesToRadians(location.Latitude)), -0.9999), 0.9999);
			point.Y = Origin.Y + 0.5 * Math.Log((1 + siny) / (1 - siny)) * -PixelsPerLonRadian;

			return point;
		}

		public static Location PointToLatLng(Point point)
		{
			var lng = (float)((point.X - Origin.X) / PixelsPerLonDegree);
			var latRadians = (point.Y - Origin.Y) / -PixelsPerLonRadian;
			var lat = (float)RadiansToDegrees(2 * Math.Atan(Math.Exp(latRadians)) - Math.PI / 2);

			return new Location() { Latitude = lat, Longitude = lng };
		}

		public static Point PointToPixel(Point point, double zoom = 0)
		{
			var pixel = new Point()
			{
				X = point.X * Math.Pow(2, zoom),
				Y = point.Y * Math.Pow(2, zoom)
			};
			return pixel;
		}

		public static Point PixelToPoint(Point point, double zoom = 0)
		{
			var pixel = new Point()
			{
				X = point.X / Math.Pow(2, zoom),
				Y = point.Y / Math.Pow(2, zoom)
			};
			return pixel;
		}

		public static Point LatLngToPixel(Location loc, double zoom)
			=> PointToPixel(LatLngToPoint(loc), zoom);
		public static Location PixelToLatLng(Point pixel, double zoom)
			=> PointToLatLng(PixelToPoint(pixel, zoom));

		public static double RadiansToDegrees(double rad)
			=> rad / (Math.PI / 180);

		public static double DegreesToRadians(double deg)
			=> deg * (Math.PI / 180);
	}
}
