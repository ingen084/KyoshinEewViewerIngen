using KyoshinMonitorLib;
using System;
using System.Windows;

namespace KyoshinEewViewer.MapControl.Projections
{
	public abstract class MapProjection
	{
		internal abstract Point LatLngToPoint(Location location);
		internal abstract Location PointToLatLng(Point point);

		public static Point PointToPixel(Point point, double zoom = 0)
			=> (Point)((Vector)point * Math.Pow(2, zoom));

		public static Point PixelToPoint(Point point, double zoom = 0)
			=> (Point)((Vector)point / Math.Pow(2, zoom));

		public Point LatLngToPixel(Location loc, double zoom)
			=> PointToPixel(LatLngToPoint(loc), zoom);
		public Location PixelToLatLng(Point pixel, double zoom)
			=> PointToLatLng(PixelToPoint(pixel, zoom));

		internal static double RadiansToDegrees(double rad)
			=> rad / (Math.PI / 180);
		internal static double DegreesToRadians(double deg)
			=> deg * (Math.PI / 180);
	}
}
