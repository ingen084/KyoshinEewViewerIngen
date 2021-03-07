using KyoshinMonitorLib;
using System;
using System.Drawing;

namespace KyoshinEewViewer.Map.Projections
{
	public abstract class MapProjection
	{
		internal abstract PointD LatLngToPoint(Location location);
		internal abstract Location PointToLatLng(PointD point);

		public static PointD PointToPixel(PointD point, double zoom = 0)
			=> new(point.X * Math.Pow(2, zoom), point.Y * Math.Pow(2, zoom));

		public static PointD PixelToPoint(PointD point, double zoom = 0)
			=> new(point.X / Math.Pow(2, zoom), point.Y / Math.Pow(2, zoom));

		public PointD LatLngToPixel(Location loc, double zoom)
			=> PointToPixel(LatLngToPoint(loc), zoom);
		public Location PixelToLatLng(PointD pixel, double zoom)
			=> PointToLatLng(PixelToPoint(pixel, zoom));

		internal static double RadiansToDegrees(double rad)
			=> rad / (Math.PI / 180);
		internal static double DegreesToRadians(double deg)
			=> deg * (Math.PI / 180);
	}
}
