using KyoshinMonitorLib;
using System;

namespace KyoshinEewViewer.Map.Projections;

public class MercatorProjection : MapProjection
{
	public const int TileSize = 256;
	private static readonly double PixelsPerLonDegree = TileSize / (double)360;
	private static readonly double PixelsPerLonRadian = TileSize / (2 * Math.PI);
	private static PointD _origin = new(128, 128);

	internal override PointD LatLngToPoint(Location location)
	{
		var siny = Math.Min(Math.Max(Math.Sin(DegreesToRadians(location.Latitude)), -0.9999), 0.9999);
		return new PointD(
			_origin.X + location.Longitude * PixelsPerLonDegree,
			_origin.Y + 0.5 * Math.Log((1 + siny) / (1 - siny)) * -PixelsPerLonRadian
		);
	}

	internal override Location PointToLatLng(PointD point)
	{
		var lng = (float)((point.X - _origin.X) / PixelsPerLonDegree);
		var latRadians = (point.Y - _origin.Y) / -PixelsPerLonRadian;
		var lat = (float)RadiansToDegrees(2 * Math.Atan(Math.Exp(latRadians)) - Math.PI / 2);

		return new Location(lat, lng);
	}
}
