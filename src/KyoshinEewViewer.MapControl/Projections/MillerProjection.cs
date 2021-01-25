using KyoshinMonitorLib;
using System;
using System.Windows;

namespace KyoshinEewViewer.MapControl.Projections
{
	public class MillerProjection : MercatorProjection
	{
		internal override Point LatLngToPoint(Location location)
		{
			var result = base.LatLngToPoint(new Location(location.Latitude * (4f / 5f), location.Longitude));
			return new Point(result.X, result.Y * (5f / 4f));
		}

		internal override Location PointToLatLng(Point point)
		{
			var result = base.PointToLatLng(new Point(point.X, point.Y * (4f / 5f)));
			return new Location(result.Latitude * (5f / 4f), result.Longitude);
		}
	}
}
