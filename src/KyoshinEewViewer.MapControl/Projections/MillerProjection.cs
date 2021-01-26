using KyoshinMonitorLib;
using System.Windows;

namespace KyoshinEewViewer.MapControl.Projections
{
	public class MillerProjection : MercatorProjection
	{
		public float Rate { get; set; } = .8f;

		internal override Point LatLngToPoint(Location location)
		{
			var result = base.LatLngToPoint(new Location(location.Latitude * Rate, location.Longitude));
			return new Point(result.X, result.Y / Rate);
		}

		internal override Location PointToLatLng(Point point)
		{
			var result = base.PointToLatLng(new Point(point.X, point.Y * Rate));
			return new Location(result.Latitude / Rate, result.Longitude);
		}
	}
}
